using System;
using System.Linq;

namespace MigrationHelper
{
    class Program
    {
        private enum ProjectType
        {
            //Libs
            CommonLib,
            Payment,

            //Hosts
            WebSite,
            Server,
            Api,
            Tasks,
        }

        private record ProjectInfo(ProjectType Type, string Path, bool IsWebApp = false);

        private class MigrationSetting
        {
            public ProjectInfo ProjectInfo { get; set; }
            public ProjectInfo[] OverrideProjectInfos { get; set; }
            public string NamespaceName { get; set; }
        }

        private static class Constants
        {
            public static readonly ProjectInfo CommonLibInfo = new ProjectInfo(ProjectType.CommonLib, "..\\CommonLib");
            public static readonly ProjectInfo PaymentInfo = new ProjectInfo(ProjectType.Payment, "..\\Payment");
            public static readonly ProjectInfo WebSiteInfo = new ProjectInfo(ProjectType.WebSite, "..\\WebSite", true);
            public static readonly ProjectInfo ServerInfo = new ProjectInfo(ProjectType.Server, "..\\..\\Server", true);
            public static readonly ProjectInfo ApiInfo = new ProjectInfo(ProjectType.Api, "..\\Api", true);
            public static readonly ProjectInfo TasksInfo = new ProjectInfo(ProjectType.Tasks, "..\\Tasks");
        }
        
        static void Main(string[] args)
        {
            var projectsToMigrate = new[]
            {
                ProjectType.CommonLib,
                ProjectType.Payment,

                ProjectType.Server,
                ProjectType.Api,
                ProjectType.Tasks,
            };
            var allowedHosts = new[]
            {
                Constants.WebSiteInfo,
                Constants.ApiInfo,
                Constants.TasksInfo,
            };

            foreach (var project in projectsToMigrate)
            {
                var setting = project switch
                {
                    ProjectType.CommonLib => new MigrationSetting
                    {
                        ProjectInfo = Constants.CommonLibInfo,
                        OverrideProjectInfos = new[]
                        {
                            Constants.WebSiteInfo,
                            Constants.ServerInfo,
                            Constants.ApiInfo,
                            Constants.TasksInfo,
                        },
                        NamespaceName = "CommonLib.JsonConfiguration"
                    },
                    ProjectType.Payment => new MigrationSetting
                    {
                        ProjectInfo = Constants.PaymentInfo,
                        OverrideProjectInfos = new[]
                        {
                            Constants.WebSiteInfo,
                            Constants.ApiInfo,
                            Constants.TasksInfo,
                        },
                        NamespaceName = "Payment.JsonConfiguration"
                    },

                    ProjectType.WebSite => new MigrationSetting
                    {
                        ProjectInfo = Constants.WebSiteInfo,
                        OverrideProjectInfos = new[]
                        {
                            Constants.WebSiteInfo,
                        },
                        NamespaceName = "DataProGroup.GenExis.UzdevumiLV.JsonConfiguration",
                    },
                    ProjectType.Server => new MigrationSetting
                    {
                        ProjectInfo = Constants.ServerInfo,
                        OverrideProjectInfos = new[]
                        {
                            Constants.ServerInfo,
                        },
                        NamespaceName = "DataProGroup.GenExis.ServerWCF.JsonConfiguration",
                    },

                    _ => throw new NotImplementedException()
                };

                setting.OverrideProjectInfos = setting.OverrideProjectInfos.Intersect(allowedHosts).ToArray();

                MigrateConfig(setting);
            }
        }

        static void MigrateConfig(MigrationSetting setting)
        {
            var sourceProjectName = setting.ProjectInfo.Type.ToString();
            var sourceConfigName = setting.ProjectInfo.IsWebApp ? "Web.config" : "app.config";
            var sourceProjectPath = setting.ProjectInfo.Path;

            // Main settings
            var mainParser = new ConfigParser(new ConfigParserSettings
                {
                    OptionsNamespace = setting.NamespaceName,
                    OptionsClassName = "CommonOptions",
                    OutputDirectoryName = sourceProjectName,
                    InformAboutUnhandled = true,
                })
                .LoadSettings($"{sourceProjectPath}\\Properties\\Settings.settings")
                .LoadConfig($"{sourceProjectPath}\\{sourceConfigName}", $"{sourceProjectName}.Properties.Settings", true);

            foreach (var over in setting.OverrideProjectInfos)
            {
                if (over == setting.ProjectInfo)
                    continue;

                var overrideConfigName = over.IsWebApp ? "Web.config" : "app.config";
                
                mainParser = mainParser
                    .LoadConfig($"{over.Path}\\{overrideConfigName}", $"{sourceProjectName}.Properties.Settings", false);
            }

            mainParser.CreateFiles();


            // Setting transformations from Host Projects
            foreach (var over in setting.OverrideProjectInfos)
            {
                var overrideFilenamePattern = over.IsWebApp ? "Web.*.config" : "app.*.config";

                new ConfigParser(new ConfigParserSettings
                    {
                        OutputDirectoryName = $"{sourceProjectName}\\From{over.Type}",
                        InformAboutUnhandled = true,
                        CreateOptions = false,
                        CreateUnhandledOutput = false,
                    })
                    .LoadSettings($"{sourceProjectPath}\\Properties\\Settings.settings")
                    .LoadConfigTransformations($"{over.Path}", overrideFilenamePattern, $"{sourceProjectName}.Properties.Settings", false);
            }
        }
    }
}
