using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using InRule.Authoring.BusinessLanguage;
using InRule.Repository;
using System.ServiceModel.Activation;
using System.Xml.Serialization;
using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using InRule.Repository.Client;
using Newtonsoft.Json.Converters;
using InRule.DevOps.Helpers;
using System.Web.Script.Serialization;
using System.Globalization;

namespace InRule.DevOps
{
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class Service : IService
    {
        public Stream GetRuleAppReport(Stream data)
        {
            string fileFullPath = GetRuleAppReportFile(data);
            Encoding localEncoding = Encoding.UTF8;

            MemoryStream mem = new MemoryStream(System.IO.File.ReadAllBytes(fileFullPath));
            string reportContent = localEncoding.GetString(mem.ToArray());
            mem.Dispose();

            return new MemoryStream(Encoding.UTF8.GetBytes(reportContent));
        }

        public string GetRuleAppReportToGitHub(Stream data)
        {
            string fileName = "testgithubreport" + System.Guid.NewGuid().ToString();
            string fileFullPath = GetRuleAppReportFile(data);
            Encoding localEncoding = Encoding.UTF8;

            MemoryStream mem = new MemoryStream(System.IO.File.ReadAllBytes(fileFullPath));
            string reportContent = localEncoding.GetString(mem.ToArray());
            mem.Dispose();

            // TODO: Does this need to support uploading to Azure Git?
            var downloadGitHubLink = GitHubHelper.Instance.UploadFileToRepo(reportContent, fileName + ".htm").Result;
            //SlackHelper.PostMessageWithDownloadButton("Click here to download rule application report from GitHub", fileName, downloadGitHubLink, "RULEAPP REPORT - ");

            return downloadGitHubLink;
        }

        private string GetRuleAppReportFile(Stream data)
        {
            StreamReader reader = new StreamReader(data);
            string ruleAppXml = reader.ReadToEnd();

            XmlDocument ruleAppDoc = new XmlDocument();
            ruleAppDoc.LoadXml(ruleAppXml);
            var ruleAppDef = RuleApplicationDef.LoadXml(ruleAppXml);

            Encoding localEncoding = Encoding.UTF8;
            TemplateEngine templateEngine = new TemplateEngine();
            templateEngine.LoadRuleApplication(ruleAppDef);
            templateEngine.LoadStandardTemplateCatalog();
            FileInfo fileInfo = InRule.Authoring.Reporting.RuleAppReport.RunRuleAppReport(ruleAppDef, templateEngine);
            templateEngine.Dispose();

            return fileInfo.FullName;
        }

        public Stream GetRuleAppDiffReport(Stream data)
        {
            string fileFullPath = GetRuleAppDiffReportFile(data);
            Encoding localEncoding = Encoding.UTF8;

            MemoryStream mem = new MemoryStream(System.IO.File.ReadAllBytes(fileFullPath));
            string reportContent = localEncoding.GetString(mem.ToArray());
            mem.Dispose();

            return new MemoryStream(Encoding.UTF8.GetBytes(reportContent));
        }

        public string GetRuleAppDiffReportToGitHub(Stream data)
        {
            string fileName = "testgithubdiffreport" + System.Guid.NewGuid().ToString();
            string fileFullPath = GetRuleAppDiffReportFile(data);
            Encoding localEncoding = Encoding.UTF8;

            MemoryStream mem = new MemoryStream(System.IO.File.ReadAllBytes(fileFullPath));
            string reportContent = localEncoding.GetString(mem.ToArray());
            mem.Dispose();

            // TODO: Does this need to support uploading to Azure Git?
            var downloadGitHubLink = GitHubHelper.Instance.UploadFileToRepo(reportContent, fileName + ".htm").Result;

            return downloadGitHubLink;
        }

        private string GetRuleAppDiffReportFile(Stream data)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(PostData));

            StreamReader reader = new StreamReader(data);

            PostData diffReportRequest = (PostData)serializer.Deserialize(data);

            string fromRuleAppXml = diffReportRequest.FromRuleAppXml;
            string toRuleAppXml = diffReportRequest.ToRuleAppXml;

            XmlDocument ruleAppDoc = new XmlDocument();
            ruleAppDoc.LoadXml(fromRuleAppXml);
            XmlNode defTag = ruleAppDoc.GetElementsByTagName("RuleApplicationDef")[0];
            var ruleAppDef = RuleApplicationDef.LoadXml(fromRuleAppXml);

            XmlDocument toRuleAppDoc = new XmlDocument();
            toRuleAppDoc.LoadXml(fromRuleAppXml);
            XmlNode toDefTag = toRuleAppDoc.GetElementsByTagName("RuleApplicationDef")[0];
            var toRuleAppDef = RuleApplicationDef.LoadXml(toRuleAppXml);

            Encoding localEncoding = Encoding.UTF8;
            TemplateEngine templateEngine = new TemplateEngine();
            templateEngine.LoadRuleApplication(ruleAppDef);
            templateEngine.LoadRuleApplication(toRuleAppDef);
            templateEngine.LoadStandardTemplateCatalog();
            FileInfo fileInfo = InRule.Authoring.Reporting.DiffReport.CreateReport(ruleAppDef, toRuleAppDef);
            templateEngine.Dispose();

            return fileInfo.FullName;
        }

        [Obsolete]
        public string ApproveRuleAppPromotion(string data)
        {
            string label = string.Empty;
            string ruleAppGuid = string.Empty;
            string repositoryUri = string.Empty;
            string revision = string.Empty;
            string approvalFlowMoniker = string.Empty;
            string message = string.Empty;

            try
            {
                data = data.Replace(" ", "+");

                var eventDataString = CryptoHelper.DecryptString(string.Empty, data);

                var obj = JsonConvert.DeserializeObject(eventDataString);
                var jObj = obj as JArray;

                var eventData = JsonConvert.DeserializeObject<List<IDictionary<string, object>>>(eventDataString);

                foreach (var item in jObj)
                {
                    if (item["Key"].ToString() == "Label")
                        label = item["Value"].ToString();

                    if (item["Key"].ToString() == "RuleAppRevision")
                        revision = item["Value"].ToString();

                    if (item["Key"].ToString() == "RepositoryUri")
                        repositoryUri = item["Value"].ToString();

                    if (item["Key"].ToString() == "GUID")
                        ruleAppGuid = item["Value"].ToString();

                    if (item["Key"].ToString() == "ApprovalFlowMoniker")
                        approvalFlowMoniker = item["Value"].ToString();
                }

                message = $"Label {label} has been approved by user{(SettingsManager.Get($"{approvalFlowMoniker}.ApplyLabelApprover").Split(' ').Length > 1 ? "s " : " ")}{SettingsManager.Get($"{approvalFlowMoniker}.ApplyLabelApprover")}.";

                using (RuleCatalogConnection connection = new RuleCatalogConnection(new Uri(repositoryUri), new TimeSpan(0, 10, 0), SettingsManager.Get("CatalogUsername"), SettingsManager.Get("CatalogPassword")))
                {
                    var ruleAppDef = connection.GetSpecificRuleAppRevision(new Guid(ruleAppGuid), int.Parse(revision));
                    var tempLabel = $"PENDING {label} ({int.Parse(revision)})";

                    if (connection.DoesLabelExist(tempLabel))
                    {
                        connection.RemoveLabel(new Guid(ruleAppGuid), int.Parse(revision), tempLabel);
                        connection.ApplyLabel(ruleAppDef, label);

                        var requesterChannelsConfig = SettingsManager.Get($"{approvalFlowMoniker}.RequesterNotificationChannel");

                        if (!string.IsNullOrEmpty(requesterChannelsConfig))
                        {
                            var requesterChannels = requesterChannelsConfig.Split(' ');
                            foreach (var channel in requesterChannels)
                            {

                                switch (SettingsManager.GetHandlerType(channel))
                                {
                                    case IHelper.InRuleEventHelperType.Teams:
                                        TeamsHelper.PostSimpleMessage(message, "APPROVAL FLOW", channel);
                                        break;
                                    case IHelper.InRuleEventHelperType.Slack:
                                        SlackHelper.PostMarkdownMessage(message, "APPROVAL FLOW", channel);
                                        break;
                                    case IHelper.InRuleEventHelperType.Email:
                                        SendGridHelper.SendEmail("APPLY LABEL REQUEST APPROVED", message, "", channel).Wait();
                                        break;
                                }
                            }
                        }
                    }
                    else
                    {
                        return "ERROR APPLYING LABEL: This link has been used already.";
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR APPLYING LABEL: " + ex.Message + "\r\n" + ex.StackTrace;
            }
            return "SUCCESS: " + message;
        }

        public Stream ProcessInRuleEvent(Stream data)
        {
            StreamReader reader = new StreamReader(data);
            string request = reader.ReadToEnd();
            dynamic eventDataSource = JsonConvert.DeserializeObject<ExpandoObject>(request, new ExpandoObjectConverter());
            var requestData = (IDictionary<string, object>)eventDataSource;

            object encryptedData = null;
            if (requestData.TryGetValue("data", out encryptedData))
            {
                var eventDataString = encryptedData.ToString().Replace(" ", "+");
                eventDataString = CryptoHelper.DecryptString("", eventDataString).Replace("InRule DevOps - ", "");

                JavaScriptSerializer serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = eventDataString.Length
                };
                var jsonDeserialized = serializer.Deserialize<IEnumerable<IDictionary<string, object>>>(eventDataString);
                var eventData = go(jsonDeserialized);

                try
                {
                    HandleAfterCallAsync(eventData);
                }
                catch (Exception ex)
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes(ex.Message));
                }
            }
            return new MemoryStream(Encoding.UTF8.GetBytes(request));
        }

        public string ProcessInRuleEventI(string data)
        {
            data = data.Replace(" ", "+");
            var eventDataString = CryptoHelper.DecryptString("", data).Replace("InRule DevOps - ", "");

            var jsonDeserialized = new JavaScriptSerializer().Deserialize<IEnumerable<IDictionary<string, object>>>(eventDataString);
            var eventData = go(jsonDeserialized);

            try
            {
                HandleAfterCallAsync(eventData);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return eventDataString;
        }

        public ExpandoObject go(IEnumerable<IDictionary<string, object>> lst)
        {

            return lst.Aggregate(new ExpandoObject(),
                                      (aTotal, n) => {
                                          (aTotal as IDictionary<string, object>).Add(n["Key"].ToString(), n["Value"] is object[]? go(((object[])n["Value"]).Cast<IDictionary<string, object>>()) : n["Value"]);
                                          return aTotal;
                                      });

        }

        private void HandleAfterCallAsync(ExpandoObject eventDataSource) //, object returnValue)
        {
            try
            {
                string FilterByUser = SettingsManager.Get("FilterEventsByUser").ToLower();
                string ApplyLabelApprover = SettingsManager.Get("ApprovalFlow.ApplyLabelApprover");
                var eventData = (dynamic)eventDataSource;
                var ruleAppXml = string.Empty;
                var filterByUsers = FilterByUser.Split(' ').ToList();

                if (filterByUsers.Any(u => u.Length > 0) && eventData.OperationName != "ApplyLabelRequest")
                    if (!filterByUsers.Contains(eventData.RequestorUsername.ToString().ToLower()))
                        return;

                DateTime utcTimestamp = new DateTime();
                try
                {
                    utcTimestamp = DateTime.Parse(eventData.UtcTimestamp);
                    eventData.ProcessingTimeInMs = (DateTime.UtcNow - utcTimestamp).TotalMilliseconds;
                }
                catch
                {
                    utcTimestamp = eventData.UtcTimestamp;
                    eventData.ProcessingTimeInMs = DateTime.UtcNow.Millisecond - utcTimestamp.Millisecond;
                }

                //DateTime.Parse(eventData.UtcTimestamp);
                //DateTime utcTimestamp = eventData.UtcTimestamp; 
                //DateTime utcTimestamp = DateTime.ParseExact(eventData.UtcTimestamp, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                //DateTime utcTimestamp = DateTime.Parse(eventData.UtcTimestamp);
                //eventData.ProcessingTimeInMs = DateTime.UtcNow.Millisecond - utcTimestamp.Millisecond;
                //eventData.ProcessingTimeInMs = (DateTime.UtcNow - utcTimestamp).TotalMilliseconds;
                if (((IDictionary<string, object>)eventDataSource).ContainsKey("RuleAppXml"))
                    ruleAppXml = eventData.RuleAppXml;

                InRuleEventHelper.ProcessEventAsync(eventData, ruleAppXml).Wait();
                return;
            }
            catch (Exception ex)
            {
                NotificationHelper.NotifyAsync("Error processing data in AfterCall (DevOps Service): " + ex.Message, "AFTER CALL EVENT - ", "Debug").Wait();
            }
            //});
        }
        private void LoadRuleAppNameFromXml(ExpandoObject eventData, string ruleAppXml)
        {
            try
            {
                XmlDocument ruleAppDoc = new XmlDocument();
                ruleAppDoc.LoadXml(ruleAppXml);
                XmlNode defTag = ruleAppDoc.GetElementsByTagName("RuleApplicationDef")[0];
                ((dynamic)eventData).Name = defTag.Attributes["Name"].Value;
            }
            catch (Exception ex)
            {
                // Lighter-weight log, because this isn't that significant
                Console.WriteLine("Error retrieving RuleApplicationDef Name attribute: " + ex.Message);
            }
        }

        public Stream RunTestsInGitHubForRuleapp(Stream ruleAppXmlStream)
        {
            StreamReader reader = new StreamReader(ruleAppXmlStream);
            string ruleAppXml = reader.ReadToEnd();
            var ruleAppDef = RuleApplicationDef.LoadXml(ruleAppXml);

            TestSuiteRunnerHelper.RunRegressionTestsAsync(ruleAppDef).Wait();

            var reportContent = "Success.";
            return new MemoryStream(Encoding.UTF8.GetBytes(reportContent));
        }
    }
}
