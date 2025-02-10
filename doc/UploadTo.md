### Configuring UploadTo Options - GitHub and Box.com

Some of the DevOps actions either require or have the option to upload files to a location accessible over the Internet. The choices we offer for now are GitHub, Azure Git, and Box.com. This page is a guide for how to configure each.

Once the configuration for GitHub, Azure Git, and/or Box.com exists, it can be referenced from UploadTo entries of sections that generate files. For example, this configuration parameter triggers the upload of the generated Java JAR file to both the GitHub and Box.com locations, using the specifications in their corresponding configuration sections:  

  <add key="Java.UploadTo" value="GitHub Box"/>


---
#### GitHub Configuration

As with all other configuration driven actions, the moniker for the default section is "GitHub". Multiple configurations can be created and used, as long as they are defined as having a "GitHub" type using the line below. The Type entry is implied for the default section with the "GitHub" moniker:

  <add key="MyGitHub.Type" value="GitHub"/>

This is a [sample configuration](../config/InRuleDevOps_GitHub.config) with the coordinates of a GitHub location where files can be uploaded by other DevOps actions. This configuration is **applicable for a local deployment**.  **For the DevOps app service**, the configuration follows the format in the [starter cloud config file](../config/InRule.DevOps.Runtime.Service.config.json).

````
  <add key="GitHub.GitHubRepo" value="InRule/DevOps"/>
  <add key="GitHub.GitHubFolder" value="JARs"/>
  <add key="GitHub.GitHubProductName" value="MyApplication"/>
  <add key="GitHub.GitHubProductVersion" value="1"/>
  <add key="GitHub.GitHubToken" value="ghp_xxxxxxxxxxxxxxxxxxxxxxxxx"/>
````

The following sample configuration would apply for an Azure Git connection
````
    <add key="AzureDevOpsGit.Type" value="AzureGit"/>
    <add key="AzureDevOpsGit.Token" value="xxxxxxxxxxxxxxxxxxxxxx"/>
    <add key="AzureDevOpsGit.Organization" value="InRuleDevOps"/>
    <add key="AzureDevOpsGit.Project" value="ROAD"/>
    <add key="AzureDevOpsGit.Repository" value="DevOpsTestRepo"/>
    <add key="AzureDevOpsGit.BranchPrefixFolder" value="inrule-devops"/>
    <add key="AzureDevOpsGit.Folder" value="DevOpsRuleApps"/>
    <add key="AzureDevOpsGit.TestSuitesPath" value="TestSuites"/>
````

We will use this GitHub location as example for extracting the values below:  https://github.com/InRule/ProjectDevOps/tree/main/Tests

|Configuration Key | Comments
--- | ---
|GitHub.**GitHubRepo**| This would be "InRule/ProjectDevOps" in the example URI above, for owner/repository.
|GitHub.**GitHubFolder**| This is the folder where the files will be uploaded. It would be "Tests" in the example URI.
|GitHub.**GitHubProductName**| The local folder used as the temporary location for the rule application file before being sent to the irDistribution service. For the Azure deployment with DevOps service, this location is overridden with the default TEMP location for the app service.
|GitHub.**GitHubProductVersion**| The local folder used as the temporary location for saving the generated Java JAR file before upload to either GitHub or Box.com. For the Azure deployment with DevOps service, this location is overridden with the default TEMP location for the app service.
|GitHub.**GitHubToken**| Personal access tokens (PATs) are an alternative to using passwords for authentication to GitHub when using the GitHub API or the command line. Follow [these instructions](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token) to create the Personal Access Token.
