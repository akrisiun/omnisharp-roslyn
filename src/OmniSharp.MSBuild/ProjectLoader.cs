﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;

using MSB = Microsoft.Build;

namespace OmniSharp.MSBuild
{
    internal class ProjectLoader
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _globalProperties;
        private readonly MSBuildOptions _options;
        private readonly SdksPathResolver _sdksPathResolver;

        public ProjectLoader(MSBuildOptions options, string solutionDirectory, ImmutableDictionary<string, string> propertyOverrides, ILoggerFactory loggerFactory, SdksPathResolver sdksPathResolver)
        {
            _logger = loggerFactory.CreateLogger<ProjectLoader>();
            _options = options ?? new MSBuildOptions();
            _sdksPathResolver = sdksPathResolver ?? throw new ArgumentNullException(nameof(sdksPathResolver));
            _globalProperties = CreateGlobalProperties(_options, solutionDirectory, propertyOverrides, _logger);
        }

        private static Dictionary<string, string> CreateGlobalProperties(
            MSBuildOptions options, string solutionDirectory, ImmutableDictionary<string, string> propertyOverrides, ILogger logger)
        {
            var globalProperties = new Dictionary<string, string>
            {
                { PropertyNames.DesignTimeBuild, "true" },
                { PropertyNames.BuildingInsideVisualStudio, "true" },
                { PropertyNames.BuildProjectReferences, "false" },
                { PropertyNames._ResolveReferenceDependencies, "true" },
                { PropertyNames.SolutionDir, solutionDirectory + Path.DirectorySeparatorChar },

                // This properties allow the design-time build to handle the Compile target without actually invoking the compiler.
                // See https://github.com/dotnet/roslyn/pull/4604 for details.
                { PropertyNames.ProvideCommandLineArgs, "true" },
                { PropertyNames.SkipCompilerExecution, "true" }
            };

            globalProperties.AddPropertyOverride(PropertyNames.MSBuildExtensionsPath, options.MSBuildExtensionsPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.TargetFrameworkRootPath, options.TargetFrameworkRootPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.RoslynTargetsPath, options.RoslynTargetsPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.CscToolPath, options.CscToolPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.CscToolExe, options.CscToolExe, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.VisualStudioVersion, options.VisualStudioVersion, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.Configuration, options.Configuration, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.Platform, options.Platform, propertyOverrides, logger);

            return globalProperties;
        }

        public // (MSB.Execution.ProjectInstance projectInstance, ImmutableArray<MSBuildDiagnostic> diagnostics) 
            Tuple<MSB.Execution.ProjectInstance, ImmutableArray<MSBuildDiagnostic>>
            BuildProject(string filePath)
        {
            using (_sdksPathResolver.SetSdksPathEnvironmentVariable(filePath))
            {
                var evaluatedProject = EvaluateProjectFileCore(filePath);

                SetTargetFrameworkIfNeeded(evaluatedProject);

                var projectInstance = evaluatedProject.CreateProjectInstance();
                var msbuildLogger = new MSBuildLogger(_logger);
                var buildResult = projectInstance.Build(
                    targets: new string[] { TargetNames.Compile, TargetNames.CoreCompile },
                    loggers: new[] { msbuildLogger });

                var diagnostics = msbuildLogger.GetDiagnostics();

                return buildResult
                    ? new Tuple<MSB.Execution.ProjectInstance, ImmutableArray<MSBuildDiagnostic>>(projectInstance, diagnostics)
                    : new Tuple<MSB.Execution.ProjectInstance, ImmutableArray<MSBuildDiagnostic>>(null, diagnostics);
            }
        }

        public MSB.Evaluation.Project EvaluateProjectFile(string filePath)
        {
            using (_sdksPathResolver.SetSdksPathEnvironmentVariable(filePath))
            {
                return EvaluateProjectFileCore(filePath);
            }
        }

        private MSB.Evaluation.Project EvaluateProjectFileCore(string filePath)
        {
            // Evaluate the MSBuild project
            var projectCollection = new MSB.Evaluation.ProjectCollection(_globalProperties);

            var toolsVersion = _options.ToolsVersion;
            if (string.IsNullOrEmpty(toolsVersion) || Version.TryParse(toolsVersion, out _))
            {
                toolsVersion = projectCollection.DefaultToolsVersion;
            }

            toolsVersion = GetLegalToolsetVersion(toolsVersion, projectCollection.Toolsets);

            return projectCollection.LoadProject(filePath, toolsVersion);
        }

        private static void SetTargetFrameworkIfNeeded(MSB.Evaluation.Project evaluatedProject)
        {
            var targetFramework = evaluatedProject.GetPropertyValue(PropertyNames.TargetFramework);
            var targetFrameworks = PropertyConverter.SplitList(evaluatedProject.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

            // If the project supports multiple target frameworks and specific framework isn't
            // selected, we must pick one before execution. Otherwise, the ResolveReferences
            // target might not be available to us.
            if (string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks.Length > 0)
            {
                // For now, we'll just pick the first target framework. Eventually, we'll need to
                // do better and potentially allow OmniSharp hosts to select a target framework.
                targetFramework = targetFrameworks[0];
                evaluatedProject.SetProperty(PropertyNames.TargetFramework, targetFramework);
            }
            else if (!string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks.Length == 0)
            {
                targetFrameworks = ImmutableArray.Create(targetFramework);
            }
        }

        private static string GetLegalToolsetVersion(string toolsVersion, ICollection<MSB.Evaluation.Toolset> toolsets)
        {
            // It's entirely possible the the toolset specified does not exist. In that case, we'll try to use
            // the highest version available.
            var version = new Version(toolsVersion);

            bool exists = false;
            Version highestVersion = null;

            var legalToolsets = new SortedList<Version, MSB.Evaluation.Toolset>(toolsets.Count);
            foreach (var toolset in toolsets)
            {
                // Only consider this toolset if it has a legal version, we haven't seen it, and its path exists.
                if (Version.TryParse(toolset.ToolsVersion, out var toolsetVersion) &&
                    !legalToolsets.ContainsKey(toolsetVersion) &&
                    Directory.Exists(toolset.ToolsPath))
                {
                    legalToolsets.Add(toolsetVersion, toolset);

                    if (highestVersion == null ||
                        toolsetVersion > highestVersion)
                    {
                        highestVersion = toolsetVersion;
                    }

                    if (toolsetVersion == version)
                    {
                        exists = true;
                    }
                }
            }

            if (highestVersion == null)
            {
                throw new InvalidOperationException("No legal MSBuild toolsets available.");
            }

            if (!exists)
            {
                toolsVersion = legalToolsets[highestVersion].ToolsPath;
            }

            return toolsVersion;
        }
    }
}
