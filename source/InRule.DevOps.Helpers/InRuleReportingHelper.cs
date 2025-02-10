using InRule.Repository;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using InRule.Authoring.BusinessLanguage;

namespace InRule.DevOps.Helpers
{
    public class InRuleReportingHelper
    {
        public static async Task GetRuleAppReportAsync(string eventType, object data, RuleApplicationDef ruleappDef)
        {
            string NotificationChannel = SettingsManager.Get("RuleAppReport.NotificationChannel");
            string UploadTo = SettingsManager.Get("RuleAppReport.UploadTo");
            try
            {
                await NotificationHelper.NotifyAsync("Generating rule application report...", "RULEAPP REPORT", "Debug");

                Encoding LocalEncoding = Encoding.UTF8;
                TemplateEngine templateEngine = new TemplateEngine();
                templateEngine.LoadRuleApplication(ruleappDef);
                templateEngine.LoadStandardTemplateCatalog();
                FileInfo fileInfo = Authoring.Reporting.RuleAppReport.RunRuleAppReport(ruleappDef, templateEngine);
                templateEngine.Dispose();

                MemoryStream mem = new MemoryStream(File.ReadAllBytes(fileInfo.FullName));
                string reportContent = LocalEncoding.GetString(mem.ToArray());
                mem.Dispose();

                var fileName = ruleappDef.Name + "_r" + ruleappDef.Revision.ToString() + ".htm";

                var channels = NotificationChannel.Split(' ');
                var uploadChannels = UploadTo.Split(' ');
                foreach (var channel in channels)
                {
                    switch (SettingsManager.GetHandlerType(channel))
                    {
                        case IHelper.InRuleEventHelperType.Teams:
                            foreach (var uploadChannel in uploadChannels)
                            {
                                switch (SettingsManager.GetHandlerType(uploadChannel))
                                {
                                    case IHelper.InRuleEventHelperType.Box:
                                        try
                                        {
                                            var downloadLink = await BoxComHelper.UploadFile(fileName, fileInfo.FullName, uploadChannel);
                                            TeamsHelper.PostMessageWithDownloadButton("Click here to download rule application report from Box.com", fileName, downloadLink, "RULEAPP REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading report to Box.com: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.GitHub:
                                        try
                                        {
                                            var downloadLink = await GitHubHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            TeamsHelper.PostMessageWithDownloadButton("Click here to download rule application report from GitHub", fileName, downloadLink, "RULEAPP REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading report to GitHub: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.AzureGit:
                                        try
                                        {
                                            var downloadLink = await AzureGitHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            TeamsHelper.PostMessageWithDownloadButton("Click here to download rule application report from Azure Git", fileName, downloadLink, "RULEAPP REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading report to Azure Git: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                }
                            }
                            break;
                        case IHelper.InRuleEventHelperType.Slack:
                            foreach (var uploadChannel in uploadChannels)
                            {
                                switch (SettingsManager.GetHandlerType(uploadChannel))
                                {
                                    case IHelper.InRuleEventHelperType.Box:
                                        try
                                        {
                                            var downloadLink = await BoxComHelper.UploadFile(fileName, fileInfo.FullName, uploadChannel);
                                            SlackHelper.PostMessageWithDownloadButton("Click here to download rule application report from Box.com", fileName, downloadLink, "RULEAPP REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading report to Box.com: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.GitHub:
                                        try
                                        {
                                            var downloadLink = await GitHubHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            SlackHelper.PostMessageWithDownloadButton("Click here to download rule application report from GitHub", fileName, downloadLink, "RULEAPP REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading report to GitHub: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.AzureGit:
                                        try
                                        {
                                            var downloadLink = await AzureGitHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            SlackHelper.PostMessageWithDownloadButton("Click here to download rule application report from Azure Git", fileName, downloadLink, "RULEAPP REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading report to Azure Git: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                }
                            }
                            break;
                        case IHelper.InRuleEventHelperType.Email:
                            await SendGridHelper.SendEventToEmailAsync(eventType, data, " - Rule Application Report", channel, reportContent);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error generating rule application report for {eventType}: {ex.Message}", "RULEAPP REPORT", "Debug");
            }
        }

        public static async Task GetRuleAppDiffReportAsync(string eventType, object data, RuleApplicationDef fromRuleappDef, RuleApplicationDef toRuleappDef)
        {
            string NotificationChannel = SettingsManager.Get("RuleAppDiffReport.NotificationChannel");
            string UploadTo = SettingsManager.Get("RuleAppDiffReport.UploadTo");
            try
            {
                //string repositoryUri = System.ServiceModel.OperationContext.Current.RequestContext.RequestMessage.Headers.To.AbsoluteUri;
                //RuleCatalogConnection connection = new RuleCatalogConnection(new Uri(repositoryUri), new TimeSpan(0, 10, 0), "Admin", "password");

                await NotificationHelper.NotifyAsync("Generating rule application difference report...", "RULEAPP DIFF REPORT", "Debug");

                Encoding LocalEncoding = Encoding.UTF8;
                TemplateEngine templateEngine = new TemplateEngine();
                templateEngine.LoadRuleApplication(fromRuleappDef);
                templateEngine.LoadRuleApplication(toRuleappDef);
                templateEngine.LoadStandardTemplateCatalog();
                FileInfo fileInfo = InRule.Authoring.Reporting.DiffReport.CreateReport(fromRuleappDef, toRuleappDef);
                templateEngine.Dispose();

                MemoryStream mem = new MemoryStream(System.IO.File.ReadAllBytes(fileInfo.FullName));
                string reportContent = LocalEncoding.GetString(mem.ToArray());
                mem.Dispose();

                var fileName = fromRuleappDef.Name + "_r" + fromRuleappDef.Revision.ToString() + "to_r" + toRuleappDef.Revision.ToString() + ".htm";

                var channels = NotificationChannel.Split(' ');
                var uploadChannels = UploadTo.Split(' ');
                foreach (var channel in channels)
                {
                    switch (SettingsManager.GetHandlerType(channel))
                    {
                        case IHelper.InRuleEventHelperType.Teams:
                            foreach (var uploadChannel in uploadChannels)
                            {
                                switch (SettingsManager.GetHandlerType(uploadChannel))
                                {
                                    case IHelper.InRuleEventHelperType.Box:
                                        try
                                        {
                                            var downloadLink = await BoxComHelper.UploadFile(fileName, fileInfo.FullName, uploadChannel);
                                            TeamsHelper.PostMessageWithDownloadButton("Click here to download rule application difference report from Box.com", fileName, downloadLink, "RULEAPP DIFF REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading difference report to Box.com: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.GitHub:
                                        try
                                        {
                                            var downloadLink = await GitHubHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            TeamsHelper.PostMessageWithDownloadButton("Click here to download rule application difference report from GitHub", fileName, downloadLink, "RULEAPP DIFF REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading difference report to GitHub: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.AzureGit:
                                        try
                                        {
                                            var downloadLink = await AzureGitHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            TeamsHelper.PostMessageWithDownloadButton("Click here to download rule application difference report from Azure Git", fileName, downloadLink, "RULEAPP DIFF REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading difference report to Azure Git: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                }
                            }
                            break;
                        case IHelper.InRuleEventHelperType.Slack:
                            foreach (var uploadChannel in uploadChannels)
                            {
                                switch (SettingsManager.GetHandlerType(uploadChannel))
                                {
                                    case IHelper.InRuleEventHelperType.Box:
                                        try
                                        {
                                            var downloadLink = await BoxComHelper.UploadFile(fileName, fileInfo.FullName, uploadChannel);
                                            SlackHelper.PostMessageWithDownloadButton("Click here to download rule application difference report from Box.com", fileName, downloadLink, "RULEAPP DIFF REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading difference report to Box.com: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.GitHub:
                                        try
                                        {
                                            var downloadLink = await GitHubHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            SlackHelper.PostMessageWithDownloadButton("Click here to download rule application difference report from GitHub", fileName, downloadLink, "RULEAPP DIFF REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading difference report to GitHub: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                    case IHelper.InRuleEventHelperType.AzureGit:
                                        try
                                        {
                                            var downloadLink = await AzureGitHelper.Instance.UploadFileToRepo(reportContent, fileName, uploadChannel);
                                            SlackHelper.PostMessageWithDownloadButton("Click here to download rule application difference report from Azure Git", fileName, downloadLink, "RULEAPP DIFF REPORT - ", channel);
                                        }
                                        catch (Exception ex)
                                        {
                                            await NotificationHelper.NotifyAsync($"Error uploading difference report to Azure Git: {ex.Message}", "RULEAPP REPORT", "Debug");
                                        }
                                        break;
                                }
                            }
                            break;
                        case IHelper.InRuleEventHelperType.Email:
                            await SendGridHelper.SendEventToEmailAsync(eventType, data, " - Rule Application Difference Report", channel, reportContent);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error generating rule application difference report for  {eventType}: {ex.Message}", "RULEAPP DIFF REPORT", "Debug");
            }
        }
    }
}
