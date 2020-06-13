
namespace commanet
{
    public class ApplicationOptions
    {
        public const string NOPREFIX = "<no prefix>";
        [CommandLineOption(
                ShortName = "p",
                LongName = "prefix",
                IsOptional = true,
                DefaultValue = NOPREFIX,
                Description = "Service Name prefix. For example: DCA1_")]
        public string ServiceNamePrefix { get; set; } = "";
    }
}
