namespace MigrationHelper
{
    public class ConfigurationRow
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }

        public ConfigurationRow(string name, string type, string value)
        {
            Name = name;
            Type = type;
            Value = value;
        }
    }
}