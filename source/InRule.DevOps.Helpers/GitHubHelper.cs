using InRule.DevOps.Helpers.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace InRule.DevOps.Helpers
{
    public class GitHubHelper : IFileRepository
    {
        private static GitHubHelper instance = null;
        public static IFileRepository Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GitHubHelper();
                }
                return instance;
            }
        }

        public const string Prefix = "GITHUB REPOSITORY";
        private const string moniker = "GitHub";
        private bool IsCloudBased = bool.Parse(SettingsManager.Get("IsCloudBased"));

        public async Task DownloadFilesFromRepo(string fileExtension)
        {
            await DownloadFilesFromRepo(fileExtension, moniker);
        }
        public async Task DownloadFilesFromRepo(string fileExtension, string moniker)
        {
            string GitHubRepo = SettingsManager.Get($"{moniker}.GitHubRepo");
            string GitHubFolder = SettingsManager.Get($"{moniker}.GitHubFolder");
            string GitHubProductName = SettingsManager.Get($"{moniker}.GitHubProductName");
            string GitHubProductVersion = SettingsManager.Get($"{moniker}.GitHubProductVersion");
            string GitHubToken = SettingsManager.Get($"{moniker}.GitHubToken");
            string GitHubDownloadPath = SettingsManager.Get("TestSuite.TestSuitesPath");

            if (GitHubRepo.Length == 0 || GitHubProductName.Length == 0 || GitHubProductVersion.Length == 0)
                return;

            if (IsCloudBased)
            {
                var tempDirectoryPath = Environment.GetEnvironmentVariable("TEMP");
                GitHubDownloadPath = tempDirectoryPath;
            }

            try
            {
                //Clear any testsuite files from the temp location
                //ToDo: Need to make sure testsuite files from other sessions are not used
                DirectoryInfo di = new DirectoryInfo(GitHubDownloadPath);
                FileInfo[] files = di.GetFiles("*." + fileExtension)
                                     .Where(p => p.Extension == "." + fileExtension).ToArray();
                foreach (FileInfo file in files)
                    try
                    {
                        file.Attributes = FileAttributes.Normal;
                        File.Delete(file.FullName);
                    }
                    catch { }

                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubProductName, GitHubProductVersion));


                var url = $"https://api.github.com/repos/{GitHubRepo}/contents/{GitHubFolder}";

                var contentsJson = await httpClient.GetStringAsync(url);

                var contents = (JArray)JsonConvert.DeserializeObject(contentsJson);
                foreach (var file in contents)
                {
                    var fileType = (string)file["type"];
                    if (fileType == "dir")
                    {
                        var directoryContentsUrl = (string)file["url"];
                        // use this URL to list the contents of the folder
                        Console.WriteLine($"DIR: {directoryContentsUrl}");
                        await NotificationHelper.NotifyAsync($"Folder {directoryContentsUrl}.", Prefix, "Debug");
                    }
                    else if (fileType == "file" && ((string)file["name"]).EndsWith("." + fileExtension))
                    {
                        var downloadUrl = (string)file["download_url"];

                        using (var client = new System.Net.WebClient())
                        {
                            client.DownloadFile(downloadUrl, Path.Combine(GitHubDownloadPath, (string)file["name"]));
                            await NotificationHelper.NotifyAsync($"Download {(string)file["name"]}.", Prefix, "Debug");
                        }
                    }
                }
                await NotificationHelper.NotifyAsync($"Downloaded {fileExtension} files from {GitHubRepo} to {GitHubDownloadPath}.", Prefix, "Debug");
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error downloading {fileExtension} files from {GitHubRepo}.\r\n" + ex.Message, Prefix, "Debug");
            }
        }

        public async Task<string> UploadFileToRepo(string fileContent, string fileName)
        {
            return await (Task.FromResult(UploadFileToRepo(fileContent, fileName, moniker).Result));
        }
        public async Task<string> UploadFileToRepo(string fileContent, string fileName, string moniker)
        {
            string GitHubRepo = SettingsManager.Get($"{moniker}.GitHubRepo");
            string GitHubFolder = SettingsManager.Get($"{moniker}.GitHubFolder");
            string GitHubProductName = SettingsManager.Get($"{moniker}.GitHubProductName");
            string GitHubProductVersion = SettingsManager.Get($"{moniker}.GitHubProductVersion");
            string GitHubToken = SettingsManager.Get($"{moniker}.GitHubToken");

            if (GitHubRepo.Length == 0 || GitHubProductName.Length == 0 || GitHubProductVersion.Length == 0)
                return string.Empty;

            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubProductName, GitHubProductVersion));

                var url = $"https://api.github.com/repos/{GitHubRepo}/contents/{GitHubFolder}";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubProductName, GitHubProductVersion));
                    var credentials = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:", GitHubToken);
                    credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                    var bytes = Encoding.ASCII.GetBytes(fileContent);
                    var base64 = Convert.ToBase64String(bytes);

                    var sha = GetExistingFileSHA(fileName, moniker).Result;
                    if (sha.Length > 0)
                        sha = ",\"sha\":" + "\"" + sha + "\"";
                    var stringContent = new StringContent("{\"message\":\"Saved by InRule DevOps.\"" + sha + ",\"content\":\"" + base64 + "\"}",
                        Encoding.UTF8, "application/json");
                    var response = await client.PutAsync(url + "/" + fileName, stringContent);

                    dynamic d = JObject.Parse(response.Content.ReadAsStringAsync().Result);

                    await NotificationHelper.NotifyAsync($"Uploaded file to {GitHubRepo}", Prefix, "Debug");
                    return d.content.download_url;
                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error uploading {fileName} file to {GitHubRepo}.\r\n" + ex.Message, Prefix, "Debug");
            }

            return string.Empty;
        }

        private async Task<string> GetExistingFileSHA(string fileName, string moniker)
        {
            string GitHubRepo = SettingsManager.Get($"{moniker}.GitHubRepo");
            string GitHubFolder = SettingsManager.Get($"{moniker}.GitHubFolder");
            string GitHubProductName = SettingsManager.Get($"{moniker}.GitHubProductName");
            string GitHubProductVersion = SettingsManager.Get($"{moniker}.GitHubProductVersion");
            string GitHubToken = SettingsManager.Get($"{moniker}.GitHubToken");

            if (GitHubRepo.Length == 0 || GitHubProductName.Length == 0 || GitHubProductVersion.Length == 0)
                return string.Empty;

            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubProductName, GitHubProductVersion));

                var url = $"https://api.github.com/repos/{GitHubRepo}/contents/{GitHubFolder}";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubProductName, GitHubProductVersion));
                    var credentials = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:", GitHubToken);
                    credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                    var response = await client.GetAsync(url + "/" + fileName);

                    dynamic d = JObject.Parse(response.Content.ReadAsStringAsync().Result);

                    if (d.sha != null)
                        return d.sha;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error checking SHA for {fileName} file to {GitHubRepo}.\r\n" + ex.Message, Prefix, "Debug");
            }

            return string.Empty;
        }

        public async Task<string> UploadFileToRepo(Stream fileContentStream, string fileName)
        {
            return await (Task.FromResult(UploadFileToRepo(fileContentStream, fileName, moniker).Result));
        }

        public async Task<string> UploadFileToRepo(Stream fileContentStream, string fileName, string moniker)
        {
            string GitHubRepo = SettingsManager.Get($"{moniker}.GitHubRepo");
            string GitHubFolder = SettingsManager.Get($"{moniker}.GitHubFolder");
            string GitHubProductName = SettingsManager.Get($"{moniker}.GitHubProductName");
            string GitHubProductVersion = SettingsManager.Get($"{moniker}.GitHubProductVersion");
            string GitHubToken = SettingsManager.Get($"{moniker}.GitHubToken");

            if (GitHubRepo.Length == 0 || GitHubProductName.Length == 0 || GitHubProductVersion.Length == 0)
                return string.Empty;

            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubProductName, GitHubProductVersion));


                var url = $"https://api.github.com/repos/{GitHubRepo}/contents/{GitHubFolder}";

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubProductName, GitHubProductVersion));
                    var credentials = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:", GitHubToken);
                    credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(credentials));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                    var fileContent = new ByteArrayContent(new StreamContent(fileContentStream).ReadAsByteArrayAsync().Result);

                    //if (!isBinary)
                    var byteArray = new StreamContent(fileContentStream).ReadAsByteArrayAsync().Result;
                    fileContent.Headers.Add("Content-Type", "application/octet-stream");
                    string base64 = base64 = Convert.ToBase64String(byteArray);

                    var stringContent = new StringContent("{\"message\":\"Saved by InRule DevOps.\",\"content\":\"" + base64 + "\"}",
                        Encoding.UTF8, "application/json");

                    var response = await client.PutAsync(url + "/" + fileName, stringContent);

                    dynamic d = JObject.Parse(response.Content.ReadAsStringAsync().Result);

                    await NotificationHelper.NotifyAsync($"Uploaded file to {GitHubRepo}", Prefix, "Debug");
                    return d.content.download_url;
                    //var contents1 = client.GetByteArrayAsync(url).Result;
                    //System.IO.File.WriteAllBytes(path, contents1);
                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error uploading {fileName} file to {GitHubRepo}.\r\n" + ex.Message, Prefix, "Debug");
            }

            return string.Empty;
        }
    }
}
