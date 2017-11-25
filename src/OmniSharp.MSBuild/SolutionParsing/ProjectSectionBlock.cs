using System.Collections.Immutable;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed class ProjectSectionBlock : SectionBlock
    {
        private ProjectSectionBlock(string name, ImmutableArray<Property> properties)
            : base(name, properties)
        {
        }

        public static ProjectSectionBlock Parse(string headerLine, Scanner scanner)
        {
            // var (name, properties) 
            var t = ParseNameAndProperties(
                "ProjectSection", "EndProjectSection", headerLine, scanner);
            var name = t.Item1;
            var properties = t.Item2;

            return new ProjectSectionBlock(name, properties);
        }
    }
}
