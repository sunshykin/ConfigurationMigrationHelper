namespace MigrationHelper
{
    public class ConfigParserSettings
    {
        public string OptionsNamespace { get; set; }
        public string OptionsClassName { get; set; }
        public string OutputDirectoryName { get; set; }
        public bool InformAboutUnhandled { get; set; }
        public bool CreateOptions { get; set; } = true;
        public bool CreateJson { get; set; } = true;
        public bool CreateUnhandledOutput { get; set; } = true;
        public string OptionsFileName { get; set; } = "Options.cs";
        public string JsonFileName { get; set; } = "settings.json";
        public string UnhandledOutputFileName { get; set; } = "unhandledSettings.txt";

        public ConfigParserSettings Clone()
        {
            return (ConfigParserSettings) MemberwiseClone();
        }
    }
}