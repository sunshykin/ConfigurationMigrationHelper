using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MigrationHelper
{
    public class ConfigParser
    {
        private const string ConfigRegexPattern =
            "<setting\\s*\\w*name=\"(\\w*)\"[^<]*serializeAs=\"(\\w*)\"[^<]*>\\s*(<value>([^<]*)<\\/value>|<value[ ]*\\/>)\\s*<\\/setting>";
        private const string SettingRegexPattern =
            "<Setting\\s*\\w*Name=\"(\\w*)\"\\s*\\w*Type=\"([\\w.]*)\"[\\s\\w.=()\"]*>\\s*(<Value[\\s\\w.=()\"]*>([^<]*)<\\/Value>|<Value [\\s\\w.=()\"]*\\/>)\\s*<\\/Setting>";
        private const string ConfigSectionRegexPatterTemplate =
            "<[ \\w.]*{0}[ \\w.]*>([\\s\\S]*)<\\/[ \\w.]*{0}[ \\w.]*>";

        private const string ResultsDirectoryName = "ConfigResults";

        private const string OptionsStartTemplate = "using System;\n\nnamespace {0}\n{{\n    public class {1}\n    {{\n";
        private const string OptionsEnd = "    }\n}";
        private const string OptionFieldTemplate = "        public {0} {1} {{ get; set; }}\n";

        private const string JsonStart = "{";
        private const string JsonEnd = "\n}";
        private const string JsonFieldTemplate = "\n    \"{0}\": {1},";

        private readonly ConfigParserSettings _settings;
        private Dictionary<string, ConfigurationRow> _values;
        private readonly HashSet<string> _unhandledValues;

        public ConfigParser(ConfigParserSettings settings)
        {
            _settings = settings;
            _values = new Dictionary<string, ConfigurationRow>();
            _unhandledValues = new HashSet<string>();
        }

        #region Private methods
        private void CreateFile(string fileName, string fileContent)
        {
            var directoryPath = PathHelper.CombineRelativePath(ResultsDirectoryName, _settings.OutputDirectoryName);

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(Path.Combine(directoryPath, fileName), fileContent);
        }

        private (bool CanBeFormatted, string Result) FormatJsonValue(string value, string type)
        {
            value ??= String.Empty;

            var escapedValue = value
                .Replace("\\", "\\\\")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&")
                .Replace("&quot;", "\"")
                .Replace("&apos;", "'");

            return type switch
            {
                // Known system types
                //var t when t == "System.String" && value.StartsWith('[') => (true, escapedValue),
                "System.String" => (true, UseBrackets(escapedValue)),
                "System.Int32" => (true, escapedValue),
                "System.Boolean" => (true, escapedValue.ToLower()),
                "System.Collections.Specialized.StringCollection" => (false, default),
                "System.TimeSpan" => (true, UseBrackets(escapedValue)),
                "System.Guid" => (true, UseBrackets(escapedValue)),
                "System.Decimal" => (true, escapedValue),

                // Known user types
                "YaKlass.GlobalSite.PortalCommon.Helpers.CustomActionLogRepositoryType" => (true, UseBrackets(escapedValue)),
                "YaKlass.GlobalSite.PortalCommon.Helpers.ProjectType" => (true, UseBrackets(escapedValue)),
                "DataProGroup.GenExis.UzdevumiLV.PaymentBLL.YandexKassa.YandexKassaTaxSystem" => (true, UseBrackets(escapedValue)),
                "DataProGroup.GenExis.UzdevumiLV.PaymentBLL.YandexKassa.YandexKassaItemTax" => (true, UseBrackets(escapedValue)),
                "YaKlass.N2Extensions.FileSystemProviderEnum" => (true, UseBrackets(escapedValue)),

                // Unknown system types
                var t when t.StartsWith("System") => throw new ArgumentException($"Type {t} is unhandled", type),

                // Unknown user types
                _ => (false, default)
            };
        }

        private string UseBrackets(string value) => $"\"{value}\"";

        private void ChangeSettingTypes(Dictionary<string, ConfigurationRow> defaultValues)
        {
            foreach (var row in _values.Values)
            {
                if (defaultValues.TryGetValue(row.Name, out var typedRow))
                {
                    row.Type = typedRow.Type;
                }
            }
        }

        #endregion

        private ConfigParser ParseRows(string text, string regexPattern, bool resetValues = true)
        {
            var regex = new Regex(regexPattern);
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                var row = new ConfigurationRow(match.Groups[1].Value, match.Groups[2].Value, match.Groups[4].Value);

                if (_values.TryGetValue(row.Name, out var storedRow))
                {
                    if (storedRow.Value != row.Value && resetValues)
                    {
                        storedRow.Value = row.Value;
                    }
                }
                else
                {
                    _values.Add(row.Name,
                        resetValues ? row : new ConfigurationRow(row.Name, row.Type, default)
                    );
                }
            }

            return this;
        }

        public ConfigParser LoadSettings(string relativeFilePath, bool resetValues = true)
        {
            var filePath = PathHelper.CombineRelativePath(relativeFilePath);
            var fileText = File.ReadAllText(filePath);

            return ParseRows(fileText, SettingRegexPattern, resetValues);
        }

        public ConfigParser LoadConfig(string relativeFilePath, string sectionName = null, bool resetValues = true)
        {
            var filePath = PathHelper.CombineRelativePath(relativeFilePath);
            var fileText = File.ReadAllText(filePath);

            if (!String.IsNullOrEmpty(sectionName))
            {
                var regex = new Regex(String.Format(ConfigSectionRegexPatterTemplate, sectionName));
                var match = regex.Match(fileText);

                fileText = match.Groups[1].Value;
            }

            return ParseRows(fileText, ConfigRegexPattern, resetValues);
        }

        public void LoadConfigTransformations(string relativeDirectoryPath, string fileNamePattern, string sectionName = null, bool createFolderForTransform = true)
        {
            var defaultValues = _values.ToDictionary(entry => entry.Key, entry => entry.Value);
            var defaultSettings = _settings.Clone();

            var originalFileName = fileNamePattern.Replace("*.", String.Empty);
            var files = new[] { PathHelper.CombineRelativePath(relativeDirectoryPath, originalFileName) }
                .Concat(Directory.GetFiles(PathHelper.CombineRelativePath(relativeDirectoryPath), fileNamePattern));

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var transformName = new Regex(fileNamePattern.Replace("*", "(\\S*)"))
                    .Match(fileName)
                    .Groups[1]
                    .Value;

                _values = new Dictionary<string, ConfigurationRow>();
                LoadConfig(Path.Combine(relativeDirectoryPath, fileName), sectionName, true);
                ChangeSettingTypes(defaultValues);

                if (_settings.CreateOptions)
                {
                    _settings.OptionsClassName = $"{defaultSettings.OptionsClassName}_{transformName}";
                }

                if (createFolderForTransform)
                {
                    _settings.OutputDirectoryName = $"{defaultSettings.OutputDirectoryName}\\{transformName}";
                }
                else
                {
                    _settings.OptionsFileName = String.IsNullOrEmpty(transformName)
                        ? defaultSettings.OptionsFileName
                        : $"Options.{transformName}.cs";
                    _settings.JsonFileName = String.IsNullOrEmpty(transformName)
                        ? defaultSettings.JsonFileName
                        : $"settings.{transformName}.json";
                    _settings.UnhandledOutputFileName = String.IsNullOrEmpty(transformName)
                        ? defaultSettings.UnhandledOutputFileName
                        : $"unhandledSettings.{transformName}.txt";
                }

                CreateFiles();
            }
        }

        public void CreateFiles()
        {
            var optionsBuilder = new StringBuilder(String.Format(OptionsStartTemplate, _settings.OptionsNamespace, _settings.OptionsClassName));
            var jsonBuilder = new StringBuilder(JsonStart);

            foreach (var setting in _values.Values.OrderBy(s => s.Name))
            {
                var (canBeFormatted, result) = FormatJsonValue(setting.Value, setting.Type);

                if (canBeFormatted)
                {
                    optionsBuilder.AppendFormat(OptionFieldTemplate, setting.Type, setting.Name);
                    jsonBuilder.AppendFormat(JsonFieldTemplate, setting.Name, result);
                }
                else
                {
                    _unhandledValues.Add(setting.Name);
                }
            }

            optionsBuilder.Append(OptionsEnd);
            jsonBuilder.Append(JsonEnd);
            var output = GetUnhandledSettingsOutput();

            if (_settings.CreateOptions)
            {
                CreateFile(_settings.OptionsFileName, optionsBuilder.ToString());
            }
            if (_settings.CreateJson)
            {
                CreateFile(_settings.JsonFileName, jsonBuilder.ToString());
            }
            if (_settings.CreateUnhandledOutput)
            {
                CreateFile(_settings.UnhandledOutputFileName, output);
            }

            if (_settings.InformAboutUnhandled)
            {
                Console.Write(output);
            }
        }

        public string GetUnhandledSettingsOutput()
        {
            if (!_unhandledValues.Any())
                return String.Empty;

            var outputBuilder = new StringBuilder($"\nUnhandled settings for {_settings.OutputDirectoryName}\\{_settings.JsonFileName}:\n");
            foreach (var name in _unhandledValues)
            {
                outputBuilder.AppendLine(name);
            }

            return outputBuilder.ToString();
        }
    }
}