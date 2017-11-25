using System.Collections.Immutable;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed class GlobalSectionBlock : SectionBlock
    {
        private GlobalSectionBlock(string name, ImmutableArray<Property> properties)
            : base(name, properties)
        {
        }

        public static GlobalSectionBlock Parse(string headerLine, Scanner scanner)
        {
            // var (name, properties) = 
            var t = ParseNameAndProperties(
                "GlobalSection", "EndGlobalSection", headerLine, scanner);
            var name = t.Item1;
            var properties = t.Item2;

            return new GlobalSectionBlock(name, properties);
        }
    }
}
