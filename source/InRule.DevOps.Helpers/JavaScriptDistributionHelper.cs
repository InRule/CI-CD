using InRule.Repository;
using InRule.Repository.Service;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static InRule.DevOps.Helpers.InRuleEventHelper;

namespace InRule.DevOps.Helpers
{
    public static class JavaScriptDistributionHelper
    {
        #region ConfigParams 
        private static readonly string moniker = "JavaScript";

        #endregion

        public static async Task CallDistributionServiceAsync(RuleApplicationDef ruleApplicationRef, bool htmlResults, bool postToBox, bool postToGitHub)
        {
            await CallDistributionServiceAsync(ruleApplicationRef, htmlResults, postToBox, postToGitHub, moniker);
        }

        public static async Task CallDistributionServiceAsync(RuleApplicationDef ruleApplicationRef, bool htmlResults, bool postToBox, bool postToGitHub, string moniker)
        {
            bool IsCloudBased = bool.Parse(SettingsManager.Get("IsCloudBased"));
            string ApiKey = SettingsManager.Get($"{moniker}.JavaScriptDistributionApiKey");
            string BaseAddress = SettingsManager.Get($"{moniker}.JavaScriptDistributionUri");
            string DestinationPath = SettingsManager.Get($"{moniker}.JavaScriptDistributionOutPath");
            //public static string RuleAppPath = SettingsManager.Get("JavaDistributionInPath");
            string NotificationChannel = SettingsManager.Get($"{moniker}.NotificationChannel");
            string UploadTo = SettingsManager.Get($"{moniker}.UploadTo");
            string jscramblerEnable = SettingsManager.Get($"{moniker}.JscramblerEnable").ToLower();
            string snowflakeEnable = SettingsManager.Get($"{moniker}.SnowflakeEnable").ToLower();

            TimeSpan SleepTimeout = TimeSpan.FromSeconds(15);
            string Prefix = "JAVASCRIPT DISTRIBUTION CHECK";

            var channels = NotificationChannel.Split(' ');
            var uploadChannels = UploadTo.Split(' ');
            if (ApiKey.Length == 0 || BaseAddress.Length == 0) // || DestinationPath.Length == 0)
                return;

            if (IsCloudBased)
            {
                var tempDirectoryPath = Environment.GetEnvironmentVariable("TEMP");
                DestinationPath = tempDirectoryPath;
            }

            string htmlContent = string.Empty;

            using (var client = new HttpClient())
            using (var requestContent = new MultipartFormDataContent())
            {
                HttpResponseMessage result = null;
                try
                {
                    client.BaseAddress = new Uri(BaseAddress);

                    // Build up our request by reading in the rule application
                    var ruleApplication = ruleApplicationRef;
                    var httpContent = new ByteArrayContent(Encoding.UTF8.GetBytes(ruleApplication.GetXml()));
                    requestContent.Add(httpContent, "ruleApplication", ruleApplication.Name + ".ruleapp");

                    if (htmlResults)
                        htmlContent = "<html><body><h2>" + $"<b>JavaScript file is being generated for {ruleApplication.Name}...</b>" + "</h2>";

                    await NotificationHelper.NotifyAsync($"JavaScript file is being generated for {ruleApplication.Name}...", Prefix, "Debug");

                    // Tell the server we are sending form data
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("multipart/form-data"));
                    client.DefaultRequestHeaders.Add("subscription-key", ApiKey);

                    // Post the rule application to the irDistribution service API,
                    // enabling Execution and State Change logging, Display Name metadata, and the developer example.
                    var distributionUrl = "package?logOption=Execution&logOption=StateChanges&subscriptionkey=" + ApiKey;
                    result = await client.PostAsync(distributionUrl, requestContent).ConfigureAwait(false);

                    // Get the return package from the result

                    dynamic returnPackage = JObject.Parse(await result.Content.ReadAsStringAsync());
                    var errors = new StringBuilder();
                    if (returnPackage.Status.ToString() == "Fail")
                    {
                        if (htmlResults)
                            htmlContent += $"<br>JavaScript file generation failed with status code: {(int)result.StatusCode}<br>";

                        await NotificationHelper.NotifyAsync($"JavaScript file generation failed with status code: {(int)result.StatusCode}", Prefix, "Debug");

                        if (returnPackage.Errors.Count > 0)
                        {
                            if (htmlResults)
                                htmlContent += $"<br><b>Errors converting ruleapp to JavaScript</b><br>";
                            errors.AppendLine("*Errors converting ruleapp to JavaScript*");

                            foreach (var error in returnPackage.Errors)
                            {
                                if (htmlResults)
                                    htmlContent += $"<br><b>{error.Description.ToString()}<br>";

                                // Handle errors
                                errors.AppendLine(">" + error.Description.ToString());
                            }
                        }

                        if (returnPackage.UnsupportedFeatures.Count > 0)
                        {
                            if (htmlResults)
                                htmlContent += $"<br><b>Unsupported features converting ruleapp to JavaScript</b><br>";
                            errors.AppendLine("*Unsupported features converting ruleapp to JavaScript*");

                            foreach (var unsupportedError in returnPackage.UnsupportedFeatures)
                            {
                                if (htmlResults)
                                    htmlContent += $"<br><b>{unsupportedError.Feature.ToString()}<br>";

                                // Handle errors
                                errors.AppendLine(">" + unsupportedError.Feature.ToString());
                            }
                            // Still need to stop processing
                        }
                    }
                    else
                    {
                        var downloadUrl = returnPackage.PackagedApplicationDownloadUrl.ToString();
                        HttpResponseMessage resultDownload = await client.GetAsync(downloadUrl);
                        if (!resultDownload.IsSuccessStatusCode)
                        {
                            errors.AppendLine(await resultDownload.Content.ReadAsStringAsync());
                        }
                        var jsContent = await resultDownload.Content.ReadAsStringAsync();
                        var fileName = ruleApplication.Name + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".js";
                        var filePath = Path.Combine(DestinationPath, fileName);

                        if (jscramblerEnable == "true")
                        {
                            try
                            {
                                await NotificationHelper.NotifyAsync($"Jscrambler process start.", Prefix, "Debug");
                                await JscramblerHelper.CallJscramblerAPIAsync(jsContent, DestinationPath, fileName);
                                jsContent = File.ReadAllText(filePath);
                            }
                            catch (Exception ex)
                            {
                                await NotificationHelper.NotifyAsync($"Jscramble error: {ex.Message}", Prefix, "Debug");
                            }

                        }
                        
                        if (snowflakeEnable == "true")
                        {
                            try
                            {
                                await NotificationHelper.NotifyAsync($"Snowflake process start.", Prefix, "Debug");
                                await SnowflakeHelper.CallSnowflakeHelper(jsContent, ruleApplicationRef);
                            }
                            catch (Exception ex)
                            {
                                await NotificationHelper.NotifyAsync($"Snowflake error: {ex.Message}", Prefix, "Debug");
                            }
                        }

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
                                        downloadLink = await GitHubHelper.Instance.UploadFileToRepo(jsContent, fileName, uploadChannel);
                                        displaySource = "GitHub";
                                        break;

                                    case UploadChannel.AzureGit:
                                        downloadLink = await AzureGitHelper.Instance.UploadFileToRepo(jsContent, fileName, uploadChannel);
                                        displaySource = "Azure Git";
                                        break;

                                    case UploadChannel.Box:
                                        downloadLink = await BoxComHelper.UploadFile(fileName, filePath, uploadChannel);
                                        displaySource = "GitHub";
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                await NotificationHelper.NotifyAsync($"Error uploading JavaScript file to {channelType}: {ex.Message}", Prefix, "Debug");
                            }

                            if (downloadLink != null && displaySource != null)
                            {
                                if (htmlResults)
                                    htmlContent += $"<br>" + $"JavaScript file has been generated for {ruleApplication.Name}. <a href=\"{downloadLink}\">Click here to download the JavaScript file {fileName} from {displaySource}</a><br></body></html>";

                                try
                                {
                                    foreach (var channel in channels)
                                    {
                                        switch (SettingsManager.GetHandlerType(channel))
                                        {
                                            case IHelper.InRuleEventHelperType.Teams:
                                                TeamsHelper.PostMessageWithDownloadButton($"JavaScript file has been generated for {ruleApplication.Name}. Click here to download the JavaScript file from {displaySource}",
                                                    ruleApplication.Name + ".js", downloadLink, Prefix, channel);
                                                break;
                                            case IHelper.InRuleEventHelperType.Slack:
                                                SlackHelper.PostMessageWithDownloadButton($">JavaScript file has been generated for {ruleApplication.Name}. Click here to download the JavaScript file from {displaySource}",
                                                    ruleApplication.Name + ".js", downloadLink, Prefix, channel);
                                                break;
                                            case IHelper.InRuleEventHelperType.Email:
                                                await SendGridHelper.SendEmail($"InRule DevOps - JavaScript rule application", string.Empty, $">JavaScript file has been generated for {ruleApplication.Name}. <a href=\"{downloadLink}\">Click here to download the JavaScript file {fileName} from {displaySource}</a>", channel);
                                                break;
                                            case IHelper.InRuleEventHelperType.EventLog:
                                                EventLog.WriteEntry("Application", $"JavaScript file has been generated for {ruleApplication.Name}. JavaScript file from {displaySource} at {downloadLink}");
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

                    if (htmlResults)
                        htmlContent += "</body></html>";

                    foreach (var channel in channels)
                    {
                        switch (SettingsManager.GetHandlerType(channel))
                        {
                            case IHelper.InRuleEventHelperType.Teams:
                                if (errors.ToString().Length > 0)
                                    TeamsHelper.PostSimpleMessage($"Conversion errors: {errors}", Prefix, channel);
                                break;
                            case IHelper.InRuleEventHelperType.Slack:
                                if (errors.ToString().Length > 0)
                                    SlackHelper.PostMarkdownMessage($"Conversion errors: {errors}", Prefix, channel);
                                break;
                            case IHelper.InRuleEventHelperType.Email:
                                await SendGridHelper.SendEmail($"InRule DevOps - JavaScript rule application", (htmlResults ? string.Empty : errors.ToString()), (htmlResults ? htmlContent : string.Empty), channel);
                                break;
                            case IHelper.InRuleEventHelperType.EventLog:
                                EventLog.WriteEntry("Application", $"InRule DevOps - JavaScript rule application {(htmlResults ? htmlContent : errors.ToString())}");
                                break;
                        }
                    }
                }
                catch (InRuleCatalogException icex)
                {
                    await NotificationHelper.NotifyAsync("Error retrieving Rule Application to compile: " + icex.Message, Prefix, "Debug");
                    //return null;
                }
                catch (JsonReaderException)
                {
                    if (result != null)
                        await NotificationHelper.NotifyAsync("Error requesting compiled Rule Application: " + await result.Content.ReadAsStringAsync(), Prefix, "Debug");
                    else
                        Console.WriteLine("Error requesting compiled Rule Application.");
                }
                catch (Exception ex)
                {
                    await NotificationHelper.NotifyAsync("CallDistributionServiceAsync error: " + ex.Message, Prefix, "Debug");
                    throw new Exception("CallDistributionServiceAsync failed", ex);
                }
            }
        }
    }
}
