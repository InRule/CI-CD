using InRule.DevOps.Helpers.Models;
using InRule.Repository;
using InRule.Repository.Regression;
using InRule.Runtime.Testing.Regression;
using InRule.Runtime.Testing.Regression.Runtime;
using InRule.Runtime.Testing.Session;
using System;
using System.IO;
using System.Threading.Tasks;


namespace InRule.DevOps.Helpers
{
    public static class TestSuiteRunnerHelper
    {
        private static readonly string moniker = "TestSuite";
        static readonly bool IsCloudBased = bool.Parse(SettingsManager.Get("IsCloudBased"));

        public static string RunInRuleTests(RuleApplicationDef ruleAppDef, IFileRepository repo, bool htmlResults)
        {
            return RunInRuleTestsAsync(ruleAppDef, repo, htmlResults, moniker).Result;
        }

        public static async Task<string> RunInRuleTestsAsync(RuleApplicationDef ruleAppDef, IFileRepository repo, bool htmlResults, string moniker)
        {
            string TestSuitesPath = SettingsManager.Get($"{moniker}.TestSuitesPath");
            string SaveResultsPath = SettingsManager.Get($"{moniker}.TestSuitesResultsPath");
            string NotificationChannel = SettingsManager.Get($"{moniker}.NotificationChannel");
            string TestSuiteGitHub = SettingsManager.Get($"{moniker}.TestSuiteGitHub");
            string UploadTo = SettingsManager.Get($"{moniker}.UploadTo");

            var channels = NotificationChannel.Split(' ');

            if (IsCloudBased)
            {
                var tempDirectoryPath = Environment.GetEnvironmentVariable("TEMP");
                TestSuitesPath = tempDirectoryPath;
                SaveResultsPath = tempDirectoryPath;
            }
            var returnValue = "InRule Regression Testing Report";

            if (htmlResults)
                returnValue = "<h2>" + returnValue + "</h2>";

            try
            {
                var testSuitePassed = true;
                var totalTested = 0;
                var totalPassed = 0;

                await NotificationHelper.NotifyAsync("CHECKING FOR TESTSUITE FILES IN " + TestSuitesPath, "REGRESSION TESTING", "Debug");

                foreach (var file in Directory.EnumerateFiles(TestSuitesPath, "*.testsuite", SearchOption.TopDirectoryOnly))
                {
                    totalTested++;
                    if (htmlResults)
                        returnValue += "<br><hr size='1'><b>TESTING: " + Path.GetFileName(file) + "</b>";
                    else
                        returnValue += "\r\n\r\n>*TESTING: " + Path.GetFileName(file) + "*\r\n";

                    try
                    {
                        TestSuitePersistenceProvider testProvider = new ZipFileTestSuitePersistenceProvider(file);
                        TestSuiteDef suite = TestSuiteDef.LoadFrom(testProvider);
                        suite.ActiveRuleApplicationDef = ruleAppDef;

                        using (TestingSessionManager manager = new TestingSessionManager(new InProcessConnectionFactory()))
                        {
                            // Create the testing session
                            RegressionTestingSession session = new RegressionTestingSession(manager, suite);

                            // Execute all Tests in the Test Suite - Ensure results collection is disposed
                            using (TestResultCollection results = session.ExecuteAllTests())
                            {
                                foreach (TestResult result in results)
                                {
                                    if (!result.Passed)
                                        testSuitePassed = false;


                                    if (htmlResults)
                                    {
                                        returnValue += "<br><b>" + result.TestDef.DisplayName + ": " + (result.Passed ? "Passed" : "Not Passed") + "<br></b>";
                                        returnValue += "<b>Runtime Error Message: </b>" + result.RuntimeErrorMessage + "<br>";
                                        returnValue += "<b>Duration: </b>" + result.Duration.ToString() + "<br>";
                                    }
                                    else
                                    {
                                        returnValue += "\r\n*" + result.TestDef.DisplayName + ": " + (result.Passed ? "Passed" : "Not Passed") + "*\r\n";
                                        returnValue += ">*Runtime Error Message:* " + result.RuntimeErrorMessage + "\r\n";
                                        returnValue += ">*Duration:* " + result.Duration.ToString() + "\r\n";
                                    }

                                    foreach (var resultAssertion in result.AssertionResults)
                                    {
                                        returnValue += ">Expected value: " + resultAssertion.ExpectedValue + ", Actual value: " + resultAssertion.ActualValue;

                                        if (htmlResults)
                                            returnValue += "<br>";
                                        else
                                            returnValue += "\r\n";
                                    }
                                }
                                // Persist TestResults to the file system
                                try
                                {
                                    string fileName = Path.GetFileName(file).Replace(".testsuite", "") + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".testresults";
                                    string fileFullName = Path.Combine(SaveResultsPath, fileName);
                                    results.SaveAs(fileFullName);

                                    MemoryStream mem = new MemoryStream(File.ReadAllBytes(fileFullName));
                                    var downloadLink = repo.UploadFileToRepo(mem, fileName, UploadTo).Result;

                                    mem.Dispose();
                                    foreach (var channel in channels)
                                    {

                                        switch (SettingsManager.GetHandlerType(channel))
                                        {
                                            case IHelper.InRuleEventHelperType.Teams:
                                                TeamsHelper.PostMessageWithDownloadButton("Click here to download test results from GitHub", fileName, downloadLink, "TESTSUITE - ", channel);
                                                break;
                                            case IHelper.InRuleEventHelperType.Slack:
                                                SlackHelper.PostMessageWithDownloadButton("Click here to download test results from GitHub", fileName, downloadLink, "TESTSUITE - ", channel);
                                                break;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        testSuitePassed = false;

                        if (htmlResults)
                            returnValue += "<br><hr size=\"1\"><b>Error testing " + Path.GetFileName(file) + ":</b> " + ex.Message;
                        else
                            returnValue += "\r\n\r\n>*Error testing " + Path.GetFileName(file) + ":* " + ex.Message + "\r\n";
                    }

                    if (testSuitePassed)
                        totalPassed++;

                    if (htmlResults)
                    {
                        returnValue += "<br><b>RESULT FOR '" + Path.GetFileName(file) + "': " + (testSuitePassed ? "PASSED" : "NOT PASSED</b><br><br>");
                    }
                    else
                        returnValue += "\r\n>*RESULT FOR '" + Path.GetFileName(file) + "':* " + (testSuitePassed ? "PASSED" : "NOT PASSED");
                }

                if (htmlResults)
                {
                    returnValue += "<br><br><b>" + totalPassed.ToString() + " / " + totalTested + " TEST SUITE FILES PASSED</b><br><br>";
                    returnValue += "</body></html>";
                }
                else
                    returnValue += "\r\n\r\n>*" + totalPassed.ToString() + " / " + totalTested + " TEST SUITE FILES PASSED*";
            }
            catch (Exception Ex)
            {
                return returnValue + Ex.Message;
            }

            return returnValue;
        }

        public static async Task RunRegressionTestsAsync(string eventType, object data, RuleApplicationDef ruleappDef)
        {
            await RunRegressionTestsAsync(eventType, data, ruleappDef, moniker);
        }

        public static async Task RunRegressionTestsAsync(string eventType, object data, RuleApplicationDef ruleappDef, string moniker)
        {
            string NotificationChannel = SettingsManager.Get($"{moniker}.NotificationChannel");
            string TestSuiteRepoMoniker = SettingsManager.Get($"{moniker}.TestSuiteGitHub");
            try
            {
                var channels = NotificationChannel.Split(' ');
                await NotificationHelper.NotifyAsync("Running unit tests in available test suites...", "REGRESSION TESTING", "Debug");

                // TODO: Does this need to support a config to run against to Azure Git?  We'd need to replace TestSuiteGitHub with TestSuiteRepoMoniker and TestSuiteRepoType
                IFileRepository repo = GitHubHelper.Instance;

                var task = repo.DownloadFilesFromRepo("testsuite", TestSuiteRepoMoniker);
                task.Wait();

                //MD ToDo: Fix so it does not run the tests twice
                var testResultsSlack = RunInRuleTestsAsync(ruleappDef, repo, false, moniker).Result;
                var testResultsEmail = RunInRuleTestsAsync(ruleappDef, repo, true, moniker).Result;

                foreach (var channel in channels)
                {
                    switch (SettingsManager.GetHandlerType(channel))
                    {
                        case IHelper.InRuleEventHelperType.Teams:
                            TeamsHelper.PostSimpleMessage(testResultsEmail, "REGRESSION TESTING", channel);
                            break;
                        case IHelper.InRuleEventHelperType.Slack:
                            SlackHelper.PostMarkdownMessage(">" + testResultsSlack, "REGRESSION TESTING", channel);
                            break;
                        case IHelper.InRuleEventHelperType.Email:
                            await SendGridHelper.SendEmail($"{((dynamic)data).OperationName} by user {((dynamic)data).RequestorUsername} - Regression Testing Results", null, testResultsEmail, channel);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public static async Task RunRegressionTestsAsync(RuleApplicationDef ruleappDef)
        {
            await RunRegressionTestsAsync(ruleappDef, moniker);
        }

        public static async Task RunRegressionTestsAsync(RuleApplicationDef ruleappDef, string moniker)
        {
            string NotificationChannel = SettingsManager.Get($"{moniker}.NotificationChannel");
            string TestSuiteRepoMoniker = SettingsManager.Get($"{moniker}.TestSuiteGitHub");

            try
            {
                var channels = NotificationChannel.Split(' ');
                await NotificationHelper.NotifyAsync("*Running unit tests in available test suites...*", "REGRESSION TESTING", "Debug");

                // TODO: Does this need to support a config to run against to Azure Git?  We'd need to replace TestSuiteGitHub with TestSuiteRepoMoniker and TestSuiteRepoType
                IFileRepository repo = GitHubHelper.Instance;

                var task = repo.DownloadFilesFromRepo("testsuite", TestSuiteRepoMoniker);
                task.Wait();

                //MD ToDo: Fix so it does not run the tests twice
                var testResultsSlack = RunInRuleTestsAsync(ruleappDef, repo, false, moniker).Result;
                var testResultsEmail = RunInRuleTestsAsync(ruleappDef, repo, true, moniker).Result;

                foreach (var channel in channels)
                {
                    switch (SettingsManager.GetHandlerType(channel))
                    {
                        case IHelper.InRuleEventHelperType.Teams:
                            TeamsHelper.PostSimpleMessage(testResultsEmail, "REGRESSION TESTING", channel);
                            break;
                        case IHelper.InRuleEventHelperType.Slack:
                            SlackHelper.PostMarkdownMessage(testResultsSlack, "REGRESSION TESTING", channel);
                            break;
                        case IHelper.InRuleEventHelperType.Email:
                            await SendGridHelper.SendEmail($"Regression Testing Results", null, testResultsEmail, channel);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}