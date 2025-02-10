using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using InRule.DevOps.Helpers.Models;

namespace InRule.DevOps.Helpers
{
    public class AzureGitHelper : IFileRepository
    {
        private static AzureGitHelper instance = null;
        public static IFileRepository Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AzureGitHelper();
                }
                return instance;
            }
        }

        public const string Prefix = "AZUREDEVOPSGIT";
        private const string moniker = "AzureDevOpsGit";

        // Download logic (could be used for TestSuiteRunnerHelper)
        public async Task DownloadFilesFromRepo(string fileExtension)
        {
            await DownloadFilesFromRepo(fileExtension, moniker);
        }
        public async Task DownloadFilesFromRepo(string fileExtension, string moniker)
        {
            string downloadPath = SettingsManager.Get("TestSuite.TestSuitesPath");
            string folder = SettingsManager.Get($"{moniker}.Folder");
            bool isCloudBased = bool.Parse(SettingsManager.Get("IsCloudBased") ?? "true");

            var connection = GetConnection();
            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
            var repo = await GetGitRepo(connection);

            try
            {
                if (isCloudBased)
                {
                    var tempDirectoryPath = Environment.GetEnvironmentVariable("TEMP");
                    downloadPath = tempDirectoryPath;
                }

                //Clear any testsuite files from the temp location
                //ToDo: Need to make sure testsuite files from other sessions are not used
                try
                {
                    DirectoryInfo di = new DirectoryInfo(downloadPath);
                    if (!di.Exists)
                    {
                        di.Create();
                    }

                    FileInfo[] files = di.GetFiles("*." + fileExtension)
                                         .Where(p => p.Extension == "." + fileExtension).ToArray();
                    foreach (FileInfo file in files)
                        try
                        {
                            file.Attributes = FileAttributes.Normal;
                            File.Delete(file.FullName);
                        }
                        catch { }
                }
                catch { }

                List<GitItem> items = await gitClient.GetItemsAsync(repo.Id, recursionLevel: VersionControlRecursionType.Full);
                foreach (GitItem itemDesc in items.Where(i => i.GitObjectType == GitObjectType.Blob && i.Path.EndsWith("." + fileExtension)))
                {
                    GitItem item = await gitClient.GetItemAsync(repo.Id, itemDesc.Path, includeContent: true);
                    var filename = itemDesc.Path.Split('/').Last();
                    File.WriteAllText(Path.Combine(downloadPath, filename), item.Content);
                    await NotificationHelper.NotifyAsync($"Downloaded {filename}.", Prefix, "Debug");
                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error downloading {fileExtension} files.\r\n" + ex.Message, Prefix, "Debug");
            }
        }

        // Upload Helper accessor methods
        public async Task<string> UploadFileToRepo(Stream fileContentStream, string fileName)
        {
            return await UploadFileToRepo(fileContentStream, fileName, moniker);
        }
        public async Task<string> UploadFileToRepo(Stream fileContentStream, string fileName, string moniker)
        {
            var fileContent = new ByteArrayContent(new StreamContent(fileContentStream).ReadAsByteArrayAsync().Result);
            var byteArray = new StreamContent(fileContentStream).ReadAsByteArrayAsync().Result;
            fileContent.Headers.Add("Content-Type", "application/octet-stream");
            string base64 = Convert.ToBase64String(byteArray);
            return await UploadFileToRepoInternal(base64, fileName, moniker);
        }
        public async Task<string> UploadFileToRepo(string fileContent, string fileName)
        {
            return await UploadFileToRepo(fileContent, fileName, moniker);
        }
        public async Task<string> UploadFileToRepo(string fileContent, string fileName, string moniker)
        {
            return await UploadFileToRepoInternal(Convert.ToBase64String(Encoding.ASCII.GetBytes(fileContent)), fileName, moniker);
        }

        // Upload Actual logic
        private async Task<string> UploadFileToRepoInternal(string base64string, string fileName, string moniker)
        {
            string branchPrefixFolder = SettingsManager.Get($"{moniker}.BranchPrefixFolder");
            string folder = SettingsManager.Get($"{moniker}.Folder");

            try
            {
                var connection = GetConnection();
                GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
                var repo = await GetGitRepo(connection);

                GitRef defaultBranch = gitClient.GetRefsAsync(repo.Id, filter: repo.DefaultBranch.Remove(0, "refs/".Length)).Result.First();
                string branchName = $"ruleAppUpdate-{DateTime.Now.ToString("yyyyMMdd-HHmmss")}";
                string featureBranch = repo.DefaultBranch.Replace(("/" + repo.DefaultBranch.Split('/').Last()), $"/{branchPrefixFolder}/{branchName}");
                GitRefUpdate newBranch = new GitRefUpdate()
                {
                    Name = featureBranch,
                    OldObjectId = defaultBranch.ObjectId,
                };
                GitCommitRef newCommit = new GitCommitRef()
                {
                    Comment = "Saved by InRule DevOps.",
                    Changes = new GitChange[]
                    {
                    new GitChange()
                    {
                        ChangeType = VersionControlChangeType.Add,
                        Item = new GitItem() { Path = $"/{folder}/{fileName}" },
                        NewContent = new ItemContent()
                        {
                            Content = base64string,
                            ContentType = ItemContentType.Base64Encoded,
                        },
                    }
                    },
                };

                GitPush push = await gitClient.CreatePushAsync(new GitPush()
                {
                    RefUpdates = new GitRefUpdate[] { newBranch },
                    Commits = new GitCommitRef[] { newCommit },
                }, repo.Id);

                await NotificationHelper.NotifyAsync($"Finished uploading {fileName} to Azure Git.", Prefix, "Debug");

                var uploadedFiles = await gitClient.GetItemsAsync(repo.Id, $"/{folder}/{fileName}", VersionControlRecursionType.Full, versionDescriptor: new GitVersionDescriptor() { Version = $"{branchPrefixFolder}/{branchName}" });
                if (uploadedFiles != null && uploadedFiles.Any())
                {
                    var file = uploadedFiles.First();
                    await NotificationHelper.NotifyAsync($"File may be retrieved from " + file.Url, Prefix, "Debug");
                    return file.Url;
                }

                await NotificationHelper.NotifyAsync($"File not found in branch", Prefix, "Debug");
                return null;
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error uploading {fileName} file to Azure Git.\r\n" + ex.Message, Prefix, "Debug");
                return null;
            }

        }

        // Helpers
        private VssConnection GetConnection()
        {
            string token = SettingsManager.Get($"{moniker}.Token");
            string organization = SettingsManager.Get($"{moniker}.Organization");

            VssConnection connection = new VssConnection(new Uri($"https://dev.azure.com/{organization}"), new VssBasicCredential(string.Empty, token));
            return connection;
        }
        private async Task<GitRepository> GetGitRepo(VssConnection connection)
        {
            string projectName = SettingsManager.Get($"{moniker}.Project");
            string repositoryName = SettingsManager.Get($"{moniker}.Repository");

            ProjectHttpClient projectClient = connection.GetClient<ProjectHttpClient>();
            var project = (await projectClient.GetProjects(null)).FirstOrDefault(p => p.Name == projectName);

            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
            var repo = (await gitClient.GetRepositoriesAsync(project.Id)).FirstOrDefault(r => r.Name == repositoryName);

            return repo;
        }
    }
}
