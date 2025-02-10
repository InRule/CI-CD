using InRule.Repository;
using InRule.Repository.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Web;

namespace InRule.DevOps.Helpers
{
    public class InRuleEventHelper
    {
        public enum UploadChannel
        {
            GitHub,
            AzureGit,
            Box
        }
        public enum InRuleEventHelperType
        {
            Slack,
            Teams,
            Email,
            TestSuite,
            ServiceBus,
            EventGrid,
            Java,
            JavaScript,
            AppInsights,
            Sql,
            RuleAppReport,
            RuleAppDiffReport,
            DevOps,
            EventLog,
            ApprovalFlow,
            BariumLiveCreateInstance,
            Webhook,
            SqlRuleSetMapper,
            RuleSetDbMapper,
            SaveToRepo,
            Promote
        }

        [Obsolete]
        public static async Task ProcessEventAsync(ExpandoObject eventDataSource, string ruleAppXml)
        {
            var eventData = (dynamic)eventDataSource;
            //int.TryParse(eventData.RuleAppRevision, out int ruleAppRevision);
            //eventData.RuleAppRevision = ruleAppRevision;
            try
            {
                if (eventData.OperationName == "CheckinRuleApp" || eventData.OperationName == "OverwriteRuleApp" || eventData.OperationName == "CreateRuleApp")
                    if (((IDictionary<string, object>)eventData).ContainsKey("RuleAppRevision"))
                        eventData.RuleAppRevision++;
                    else
                        ((IDictionary<string, object>)eventData).Add("RuleAppRevision", 1);

                string EventHandlers = SettingsManager.Get("On" + eventData.OperationName);
                if (string.IsNullOrEmpty(EventHandlers))
                {
                    EventHandlers = SettingsManager.Get("OnAny");
                    if (string.IsNullOrEmpty(EventHandlers))
                        return;
                }

                List<string> handlers = EventHandlers.Split(' ').ToList();
                ruleAppXml = HttpUtility.HtmlDecode(ruleAppXml);

                RuleApplicationDef ruleAppDef = null;

                // This was an attempt to make running locally more efficient.  However, ruleAppXml is not escaped and may throw an exception
                // when getting loaded (IE it includes <%text%> from Notifications un-escaped)
                /*
                if (!string.IsNullOrEmpty(ruleAppXml))
                {
                    ruleAppDef = RuleApplicationDef.LoadXml(ruleAppXml);
                }
                else
                {
                    ruleAppDef = GetRuleAppDef(eventData.RepositoryUri.ToString(), eventData.GUID.ToString(), eventData.RuleAppRevision, eventData.OperationName);
                    ruleAppXml = ruleAppDef.GetXml();
                }*/
                ruleAppDef = GetRuleAppDef(eventData.RepositoryUri.ToString(), eventData.GUID.ToString(), eventData.RuleAppRevision, eventData.OperationName);
                ruleAppXml = ruleAppDef.GetXml();


                foreach (var handler in handlers)
                {
                    try
                    {
                        InRuleEventHelperType handlerType = new InRuleEventHelperType();
                        if (Enum.IsDefined(typeof(InRuleEventHelperType), handler))
                            Enum.TryParse(handler, out handlerType);
                        else
                        {
                            string handlerTypeInConfig = SettingsManager.Get($"{handler}.Type");
                            Enum.TryParse(handlerTypeInConfig, out handlerType);
                        }

                        await NotificationHelper.NotifyAsync($"BEGIN PROCESSING {eventData.OperationName} -> {handler} ({handlerType})", string.Empty, "Debug");

                        switch (handlerType) {
                            case InRuleEventHelperType.Slack:
                                await SlackHelper.SendEventToSlackAsync(eventData.OperationName, eventData, "CATALOG EVENT", handler);
                                break;
                            case InRuleEventHelperType.Teams:
                                await TeamsHelper.SendEventToTeamsAsync(eventData.OperationName, eventData, "CATALOG EVENT", handler);
                                break;
                            case InRuleEventHelperType.Email:
                                await SendGridHelper.SendEventToEmailAsync(eventData.OperationName, eventData, " - InRule Catalog Event", string.Empty);
                                break;
                            case InRuleEventHelperType.TestSuite:
                                if (ruleAppDef != null)
                                {
                                    TestSuiteRunnerHelper.RunRegressionTestsAsync(eventData.OperationName, eventData, ruleAppDef, handler);
                                }
                                break;
                            case InRuleEventHelperType.ServiceBus:
                                var eventDataJson = JsonConvert.SerializeObject(eventData);
                                AzureServiceBusHelper.SendMessageAsync(eventDataJson, handler);
                                break;
                            case InRuleEventHelperType.EventGrid:
                                EventGridHelper.PublishEventAsync(eventData.OperationName, eventData, handler);
                                break;
                            case InRuleEventHelperType.Java:
                                if (ruleAppDef != null)
                                {
                                    await JavaDistributionHelper.GenerateJavaJar(ruleAppDef, true, handler);
                                }
                                break;
                            case InRuleEventHelperType.JavaScript:
                                if (ruleAppDef != null)
                                {
                                    await JavaScriptDistributionHelper.CallDistributionServiceAsync(ruleAppDef, true, false, true, handler);
                                }
                                break;
                            case InRuleEventHelperType.AppInsights:
                                AzureAppInsightsHelper.PublishEventToAppInsights(eventData.OperationName, eventData, handler);
                                break;
                            case InRuleEventHelperType.Sql:
                                SqlDatabaseHelper.WriteEvent(eventData.OperationName, eventData, handler);
                                break;
                            case InRuleEventHelperType.RuleAppReport:
                                if (ruleAppDef != null)
                                {
                                    await InRuleReportingHelper.GetRuleAppReportAsync(eventData.OperationName, eventData, ruleAppDef);
                                }
                                break;
                            case InRuleEventHelperType.RuleAppDiffReport:
                                if (ruleAppDef == null) continue;
                                if (ruleAppDef.Revision > 1)
                                {
                                    var fromRuleAppDef = GetRuleAppDef(eventData.RepositoryUri.ToString(), ruleAppDef.Guid.ToString(), ruleAppDef.Revision - 1, string.Empty);
                                    if (fromRuleAppDef != null)
                                        await InRuleReportingHelper.GetRuleAppDiffReportAsync(eventData.OperationName, eventData, fromRuleAppDef, ruleAppDef);
                                }
                                break;
                            case InRuleEventHelperType.DevOps:
                                AzureDevOpsApiHelper.QueuePipelineBuild(handler, ruleAppDef, eventData);
                                break;
                            case InRuleEventHelperType.EventLog:
                                EventLog.WriteEntry("InRule", JsonConvert.SerializeObject(eventData, Newtonsoft.Json.Formatting.Indented), EventLogEntryType.Information);
                                break;
                            case InRuleEventHelperType.ApprovalFlow:
                                if (eventData.RequiresApproval)
                                {
                                    eventData.ApprovalFlowMoniker = handler;
                                    eventData = (dynamic)eventDataSource;
                                    ruleAppDef = !string.IsNullOrEmpty(ruleAppXml) ? RuleApplicationDef.LoadXml(ruleAppXml) : (RuleApplicationDef)GetRuleAppDef(eventData.RepositoryUri.ToString(), eventData.GUID.ToString(), eventData.RuleAppRevision, eventData.OperationName);
                                    await CheckInApprovalHelper.SendApproveRequestAsync(eventDataSource, ruleAppDef, handler);
                                }
                                break;
                            case InRuleEventHelperType.BariumLiveCreateInstance:
                                await BariumLiveHelper.BariumLiveCreateInstance();
                                break;
                            case InRuleEventHelperType.Webhook:
                                await WebhookHelper.PostToWebhook(ruleAppXml);
                                break;
                            case InRuleEventHelperType.SqlRuleSetMapper:
                                SqlMapperHelper.MapToDatabase(eventData, ruleAppDef);
                                break;
                            case InRuleEventHelperType.RuleSetDbMapper:
                                RuleSetDbMapper.RunRuleSetDbMapper(ruleAppDef, eventData);
                                break;
                            case InRuleEventHelperType.SaveToRepo:
                                await UploadToRepoHelper.UploadToRepo(ruleAppDef, handler);
                                break;
                            case InRuleEventHelperType.Promote:
                                await PromotionHelper.Promote(ruleAppDef, handler);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        await NotificationHelper.NotifyAsync(ex.Message, "PROCESS EVENT " + eventData.OperationName + " ERROR", "Debug");
                    }
                }
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync(ex.Message, "PROCESS EVENT (ProcessEventAsync) ERROR", "Debug");
            }
        }

        private static RuleApplicationDef GetRuleAppDef(string catalogUri, string ruleAppGuid, int revision, string catalogEventName)
        {
            try
            {
                RuleCatalogConnection connection = new RuleCatalogConnection(new Uri(catalogUri), new TimeSpan(0, 10, 0), SettingsManager.Get("CatalogUsername"), SettingsManager.Get("CatalogPassword"));
                return connection.GetSpecificRuleAppRevision(new System.Guid(ruleAppGuid), revision);
            }
            catch (Exception ex)
            {
                NotificationHelper.NotifyAsync(ex.Message, "CANNOT RETRIEVE RULEAPP FROM " + catalogUri + " - ", "Debug").Wait();
            }
            return null;
        }

        private static string GetRuleAppName(string catalogUri, string ruleAppGuid)
        {
            try
            {
                RuleCatalogConnection connection = new RuleCatalogConnection(new Uri(catalogUri), new TimeSpan(0, 10, 0), SettingsManager.Get("CatalogUsername"), SettingsManager.Get("CatalogPassword"));
                return connection.GetRuleAppRef(new System.Guid(ruleAppGuid)).Name;
            }
            catch (Exception ex)
            {
                NotificationHelper.NotifyAsync(ex.Message, "CANNOT RETRIEVE RULEAPP FROM " + catalogUri + " - ", "Debug").Wait();
            }
            return null;
        }
    }
}
