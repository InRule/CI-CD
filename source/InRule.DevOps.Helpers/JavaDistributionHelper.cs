using InRule.Common.Utilities;
using InRule.Repository;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static InRule.DevOps.Helpers.InRuleEventHelper;

namespace InRule.DevOps.Helpers
{
    public static class JavaDistributionHelper
    {

        #region ConfigParams  
        private static readonly string moniker = "Java";
        #endregion

        public static async Task GenerateJavaJar(RuleApplicationDef ruleAppDef, bool htmlResults)
        {
            await GenerateJavaJar(ruleAppDef, htmlResults, moniker);
        }

        public static async Task GenerateJavaJar(RuleApplicationDef ruleAppDef, bool htmlResults, string moniker)
        {
            bool IsCloudBased = bool.Parse(SettingsManager.Get("IsCloudBased"));
            string ApiKey = SettingsManager.Get($"{moniker}.JavaDistributionApiKey");
            string BaseAddress = SettingsManager.Get($"{moniker}.JavaDistributionUri");
            string DestinationPath = SettingsManager.Get($"{moniker}.JavaDistributionOutPath");
            string RuleAppPath = SettingsManager.Get($"{moniker}.JavaDistributionInPath");
            string NotificationChannels = SettingsManager.Get($"{moniker}.NotificationChannel");
            string UploadTo = SettingsManager.Get($"{moniker}.UploadTo");
            TimeSpan SleepTimeout = TimeSpan.FromSeconds(15);
            string Prefix = "JAVA DISTRIBUTION";

            try
            {
                var channels = NotificationChannels.Split(' ');
                var uploadChannels = UploadTo.Split(' ');
                if (ApiKey.Length == 0 || BaseAddress.Length == 0)
                    return;

                if (IsCloudBased)
                {
                    var tempDirectoryPath = Environment.GetEnvironmentVariable("TEMP");
                    DestinationPath = tempDirectoryPath;
                    RuleAppPath = tempDirectoryPath;
                }

                string htmlContent = string.Empty;
                var resultContent = string.Empty;

                if (htmlResults)
                    htmlContent = "<html><body><h2>" + $"<b>Java jar file is being generated for {ruleAppDef.Name}...</b>" + "</h2>";

                await NotificationHelper.NotifyAsync($"*Java jar file is being generated for {ruleAppDef.Name}...*", Prefix, "Debug");

                var ruleAppPath = Path.Combine(RuleAppPath, Guid.NewGuid() + ".ruleappx");

                using (var zipArchive = ZipFile.Open(ruleAppPath, ZipArchiveMode.Create))
                {
                    var entry = zipArchive.CreateEntry("ruleapp/" + ruleAppDef.Name, CompressionLevel.Optimal);

                    using (Stream stream = entry.Open())
                    {
                        XmlSerializationUtility.SaveObjectToStream(stream, ruleAppDef);
                    }
                }

                HttpResponseMessage result;

                await NotificationHelper.NotifyAsync("Using rule application file for rule application " + ruleAppDef.Name, Prefix, "Debug");

                using (var ruleAppStream = File.OpenRead(ruleAppPath))
                using (var client = new HttpClient { BaseAddress = new Uri(BaseAddress) })
                {
                    var content = new MultipartFormDataContent();
                    content.Add(new StreamContent(ruleAppStream), Path.GetFileName(ruleAppPath), Path.GetFileName(ruleAppPath));

                    if (htmlResults)
                        htmlContent += "<br><hr size=\"1\">Uploading rule application...<br>";

                    await NotificationHelper.NotifyAsync("Uploading rule application...", Prefix, "Debug");

                    client.DefaultRequestHeaders.Add("x-functions-key", ApiKey);
                    result = await client.PostAsync($"package/java", content);

                    if (!result.IsSuccessStatusCode)
                    {
                        if (htmlResults)
                            htmlContent += $"<br>Upload failed with status code: {(int)result.StatusCode} {result.ReasonPhrase}<br>";

                        await NotificationHelper.NotifyAsync($"Upload failed with status code: {(int)result.StatusCode} {result.ReasonPhrase}", Prefix, "Debug");

                        resultContent = await result.Content.ReadAsStringAsync();
                        await NotificationHelper.NotifyAsync(resultContent, Prefix, "Debug");
                        return;
                    }

                    if (htmlResults)
                        htmlContent += "<br>Upload successful. Waiting for packaging to complete...<br>";

                    await NotificationHelper.NotifyAsync("Upload successful. Waiting for packaging to complete...", Prefix, "Debug");

                    var startTime = DateTime.UtcNow;
                    while (true)
                    {
                        if (DateTime.UtcNow.Subtract(startTime).TotalMinutes >= 10.0)
                        {
                            //Console.ForegroundColor = ConsoleColor.Red;
                            if (htmlResults)
                                htmlContent += "<br>Packaging into Java failed due to timeout period of 10 minutes.<br>";

                            await NotificationHelper.NotifyAsync($"Packaging into Java failed due to timeout period of 10 minutes.", Prefix, "Debug");
                            //Console.ResetColor();
                            return;
                        }

                        result = await client.GetAsync(result.Headers.Location);

                        if (result.StatusCode == HttpStatusCode.Accepted)
                        {
                            if (htmlResults)
                                htmlContent += $"<br>Status: 202 Accepted. Sleeping for {SleepTimeout.TotalSeconds} seconds...<br>";

                            await NotificationHelper.NotifyAsync($"Status: 202 Accepted. Sleeping for {SleepTimeout.TotalSeconds} seconds...", Prefix, "Debug");
                            Thread.Sleep(SleepTimeout);
                            continue;
                        }

                        if (result.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            if (htmlResults)
                                htmlContent += $"<br>Status: 503 Service Unavailable. Sleeping for 5 seconds...<br>";

                            await NotificationHelper.NotifyAsync("Status: 503 Service Unavailable. Sleeping for 5 seconds...", Prefix, "Debug");
                            Thread.Sleep(TimeSpan.FromSeconds(5));
                            continue;
                        }

                        break;
                    }
                }

                File.Delete(ruleAppPath);

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    resultContent = await result.Content.ReadAsStringAsync();
                    dynamic returnPackage = JObject.Parse(resultContent);
                    var errors1 = new StringBuilder();

                    if (htmlResults)
                        htmlContent += $"<br>Packaging into Java failed with status code: {(int)result.StatusCode} {result.ReasonPhrase}<br>";

                    await NotificationHelper.NotifyAsync($"Packaging into Java failed with status code: {(int)result.StatusCode} {result.ReasonPhrase}", Prefix, "Debug");

                    if (htmlResults)
                        htmlContent += $"<br><b>Errors converting ruleapp to Java jar</b><br>";

                    if (returnPackage.errors.Count > 0)
                    {
                        errors1.AppendLine("*Errors converting ruleapp to Java jar*");

                        foreach (var error in returnPackage.errors)
                        {
                            // Handle errors
                            errors1.AppendLine(">" + error.description.ToString());

                            if (htmlResults)
                                htmlContent += $"<br><b>{error.code}</b>: {error.description.ToString()}<br>";

                            errors1.AppendLine(">*" + error.code + ":* " + error.description);
                        }
                    }

                    if (returnPackage.unsupportedFeatures.Count > 0)
                    {
                        errors1.AppendLine("*Unsupported features converting ruleapp to Java jar*");
                        foreach (var unsupportedError in returnPackage.unsupportedFeatures)
                        {
                            if (htmlResults)
                                htmlContent += $"<br>" + unsupportedError.feature.ToString() + "<br>";

                            errors1.AppendLine(">" + unsupportedError.feature.ToString());
                        }
                    }

                    if (htmlResults)
                        htmlContent += "</body></html>";

                    foreach (var channel in channels)
                    {
                        switch (SettingsManager.GetHandlerType(channel))
                        {
                            case IHelper.InRuleEventHelperType.Teams:
                                TeamsHelper.PostSimpleMessage(errors1.ToString(), Prefix, channel);
                                break;
                            case IHelper.InRuleEventHelperType.Slack:
                                SlackHelper.PostMarkdownMessage(errors1.ToString(), Prefix, channel);
                                break;
                            case IHelper.InRuleEventHelperType.Email:
                                await SendGridHelper.SendEmail($"InRule DevOps - Java jar generation", (htmlResults ? string.Empty : errors1.ToString()), (htmlResults ? htmlContent : string.Empty), channel);
                                break;
                        }
                    }
                }
                else
                {
                    var jarStream = await result.Content.ReadAsStreamAsync();
                    var saveToFileName = result.Content.Headers.ContentDisposition.FileName;
                    var filePath = Path.Combine(DestinationPath, saveToFileName);

                    if (htmlResults)
                        htmlContent += $"<br>" + $"Packaging in Java complete. Saving the file to: {filePath}" + "<br>";

                    await NotificationHelper.NotifyAsync($"Packaging in Java complete. Saving the file to: {filePath}", Prefix, "Debug");

                    var fileName = ruleAppDef.Name + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jar";

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
                                    downloadLink = await GitHubHelper.Instance.UploadFileToRepo(jarStream, fileName, uploadChannel);
                                    displaySource = "GitHub";
                                    break;

                                case UploadChannel.AzureGit:
                                    downloadLink = await AzureGitHelper.Instance.UploadFileToRepo(jarStream, fileName, uploadChannel);
                                    displaySource = "Azure Git";
                                    break;

                                case UploadChannel.Box:
                                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                                    {
                                        await jarStream.CopyToAsync(fileStream);
                                    }
                                    downloadLink = await BoxComHelper.UploadFile(fileName, filePath, uploadChannel);
                                    displaySource = "Box.com";
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            await NotificationHelper.NotifyAsync($"Error uploading Java JAR file to {channelType}: {ex.Message}", Prefix, "Debug");
                        }

                        if (downloadLink != null && displaySource != null)
                        {
                            if (htmlResults)
                                htmlContent += $"<br>" + $"Java jar has been generated for {ruleAppDef.Name}. <a href=\"{downloadLink}\">Click here to download the Java jar file {fileName} from {displaySource}</a><br></body></html>";

                            try
                            {
                                foreach (var channel in channels)
                                {
                                    switch (SettingsManager.GetHandlerType(channel))
                                    {
                                        case IHelper.InRuleEventHelperType.Teams:
                                            TeamsHelper.PostMessageWithDownloadButton($"Java jar has been generated for {ruleAppDef.Name}. Click here to download the Java jar file from {displaySource}", ruleAppDef.Name + ".jar", downloadLink, Prefix, channel);
                                            break;
                                        case IHelper.InRuleEventHelperType.Slack:
                                            SlackHelper.PostMessageWithDownloadButton($"Java jar has been generated for {ruleAppDef.Name}. Click here to download the Java jar file from {displaySource}", ruleAppDef.Name + ".jar", downloadLink, Prefix, channel);
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

                    foreach (var channel in channels)
                    {
                        switch (SettingsManager.GetHandlerType(channel))
                        {
                            case IHelper.InRuleEventHelperType.Email:
                                await SendGridHelper.SendEmail($"InRule DevOps - Java jar generation", string.Empty, htmlContent, channel);
                                break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync("GenerateJavaJar error: " + ex.Message + "\r\n" + ex.InnerException, Prefix, "Debug");
            }
        }
    }
}
