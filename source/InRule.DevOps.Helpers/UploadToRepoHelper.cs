using InRule.Repository;
using System;
using System.Threading.Tasks;
using static InRule.DevOps.Helpers.InRuleEventHelper;

namespace InRule.DevOps.Helpers
{
    public static class UploadToRepoHelper
    {
        public static async Task UploadToRepo(RuleApplicationDef ruleAppDef, string moniker)
        {
            string UploadTo = SettingsManager.Get($"{moniker}.UploadTo");
            string NotificationChannels = SettingsManager.Get($"{moniker}.NotificationChannel");
            string Prefix = "UPLOAD TO REPO";

            try
            {
                var uploadChannels = UploadTo.Split(' ');

                foreach (var uploadChannel in uploadChannels)
                {
                    UploadChannel channelType = new UploadChannel();
                    string configType;

                    if (Enum.IsDefined(typeof(UploadChannel), uploadChannel))
                        Enum.TryParse(uploadChannel, out channelType);
                    else
                    {
                        configType = SettingsManager.Get($"{uploadChannel}.Type");
                        if (Enum.IsDefined(typeof(UploadChannel), configType))
                            Enum.TryParse(configType, out channelType);
                    }

                    string downloadLink = null;
                    string displaySource = null;
                    try
                    {
                        switch (channelType)
                        {
                            case UploadChannel.GitHub:
                                downloadLink = await GitHubHelper.Instance.UploadFileToRepo(ruleAppDef.GetXml(), ruleAppDef.Name + ".ruleapp", uploadChannel);
                                displaySource = "GitHub";
                                break;

                            case UploadChannel.AzureGit:
                                downloadLink = await AzureGitHelper.Instance.UploadFileToRepo(ruleAppDef.GetXml(), ruleAppDef.Name + ".ruleapp", uploadChannel);
                                displaySource = "Azure Git";
                                break;

                            case UploadChannel.Box:
                                downloadLink = await BoxComHelper.UploadFile(ruleAppDef.GetXml(), ruleAppDef.Name + ".ruleapp", uploadChannel);
                                displaySource = "Box.com";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        await NotificationHelper.NotifyAsync($"Error uploading Rule Application to {channelType}: {ex.Message}", Prefix, "Debug");
                    }

                    if (!string.IsNullOrEmpty(downloadLink) && !string.IsNullOrEmpty(NotificationChannels))
                    {
                        string confirmationMessage = $"Rule Application {ruleAppDef.Name} has been uploaded to {displaySource} and can be found at {downloadLink}";
                        try
                        {
                            var channels = NotificationChannels.Split(' ');
                            foreach (var channel in channels)
                            {
                                switch (SettingsManager.GetHandlerType(channel))
                                {
                                    case IHelper.InRuleEventHelperType.Teams:
                                        TeamsHelper.PostSimpleMessage(confirmationMessage, Prefix, channel);
                                        break;
                                    case IHelper.InRuleEventHelperType.Slack:
                                        SlackHelper.PostMarkdownMessage(confirmationMessage, Prefix, channel);
                                        break;
                                    case IHelper.InRuleEventHelperType.Email:
                                        await SendGridHelper.SendEmail($"InRule DevOps - Rule Application Uploaded", confirmationMessage, channel);
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            await NotificationHelper.NotifyAsync($"Error notifying channels about uploaded file: {ex.Message}", Prefix, "Debug");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync("UploadToRepo error: " + ex.Message + "\r\n" + ex.InnerException, Prefix, "Debug");
            }
        }
    }
}
