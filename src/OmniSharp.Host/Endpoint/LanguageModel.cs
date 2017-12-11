namespace OmniSharp.Endpoint
{
    public class LanguageModel
    {
        public string Language { get; set; }
        public string FileName { get; set; }
    }
}

// .Vfp
namespace OmniSharp
{

    public static class LanguageNames
    {
        public const string CSharp = "C#";
        public const string VisualBasic = "Visual Basic";
        public const string FSharp = "F#";

        public const string Vfp = "VFP";
    }
}
