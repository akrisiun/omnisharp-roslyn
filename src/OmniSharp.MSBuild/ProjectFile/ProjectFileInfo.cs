using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.MSBuild.Logging;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal partial class ProjectFileInfo
    {
        private readonly ProjectData _data;

        public string FilePath { get; }
        public string Directory { get; }

        public ProjectId Id { get; }

        public Guid Guid => _data.Guid;
        public string Name => _data.Name;

        public string AssemblyName => _data.AssemblyName;
        public string TargetPath => _data.TargetPath;
        public string OutputPath => _data.OutputPath;
        public string ProjectAssetsFile => _data.ProjectAssetsFile;

        public FrameworkName TargetFramework => _data.TargetFramework;
        public ImmutableArray<string> TargetFrameworks => _data.TargetFrameworks;

        public OutputKind OutputKind => _data.OutputKind;
        public LanguageVersion LanguageVersion => _data.LanguageVersion;
        public bool AllowUnsafeCode => _data.AllowUnsafeCode;
        public string DocumentationFile => _data.DocumentationFile;
        public IList<string> PreprocessorSymbolNames => _data.PreprocessorSymbolNames;
        public IList<string> SuppressedDiagnosticIds => _data.SuppressedDiagnosticIds;

        public bool SignAssembly => _data.SignAssembly;
        public string AssemblyOriginatorKeyFile => _data.AssemblyOriginatorKeyFile;

        public ImmutableArray<string> SourceFiles => _data.SourceFiles;
        public ImmutableArray<string> References => _data.References;
        public ImmutableArray<string> ProjectReferences => _data.ProjectReferences;
        public ImmutableArray<PackageReference> PackageReferences => _data.PackageReferences;
        public ImmutableArray<string> Analyzers => _data.Analyzers;

        private ProjectFileInfo(
            ProjectId id,
            string filePath,
            ProjectData data)
        {
            this.Id = id;
            this.FilePath = filePath;
            this.Directory = Path.GetDirectoryName(filePath);

            _data = data;
        }

        internal static ProjectFileInfo CreateEmpty(string filePath)
        {
            var id = ProjectId.CreateNewId(debugName: filePath);

            return new ProjectFileInfo(id, filePath, data: null);
        }

        internal static ProjectFileInfo CreateNoBuild(string filePath, ProjectLoader loader)
        {
            var id = ProjectId.CreateNewId(debugName: filePath);
            var project = loader.EvaluateProjectFile(filePath);
            var data = ProjectData.Create(project);

            return new ProjectFileInfo(id, filePath, data);
        }

        public static Tuple<ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>>
            // (ProjectFileInfo projectFileInfo, ImmutableArray<MSBuildDiagnostic> diagnostics) 
            Load(string filePath, ProjectLoader loader)
        {
            if (!File.Exists(filePath))
            {
                // return (null, ImmutableArray<MSBuildDiagnostic>.Empty);
                return new Tuple<ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>>(null, ImmutableArray<MSBuildDiagnostic>.Empty);
            }

            var t  = loader.BuildProject(filePath);
            var projectInstance = t.Item1; // (projectInstance, diagnostics)
            var diagnostics = t.Item2;
            if (projectInstance == null)
            {
                // return (null, diagnostics);
                return new Tuple<ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>>(null, diagnostics);
            }

            var id = ProjectId.CreateNewId(debugName: filePath);
            var data = ProjectData.Create(projectInstance);
            var projectFileInfo = new ProjectFileInfo(id, filePath, data);

            return new Tuple<ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>>(projectFileInfo, diagnostics);
        }

        public // (ProjectFileInfo projectFileInfo, ImmutableArray<MSBuildDiagnostic> diagnostics) 
            Tuple<ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>>
            Reload(ProjectLoader loader)
        {
            var t = loader.BuildProject(FilePath);
            var projectInstance = t.Item1; // (projectInstance, diagnostics) 
            var diagnostics = t.Item2;
            if (projectInstance == null)
            {
                return new Tuple<ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>>(null, diagnostics);
            }

            var data = ProjectData.Create(projectInstance);
            var projectFileInfo = new ProjectFileInfo(Id, FilePath, data);

            return new Tuple<ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>>(projectFileInfo, diagnostics);
        }

        public bool IsUnityProject()
            => References.Any(filePath =>
                {
                    var fileName = Path.GetFileName(filePath);

                    return string.Equals(fileName, "UnityEngine.dll", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(fileName, "UnityEditor.dll", StringComparison.OrdinalIgnoreCase);
                });
    }
}
