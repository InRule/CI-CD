### Start Azure DevOps Build Pipeline with a Catalog Event

As the [Microsoft® documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/?view=azure-devops) describes them, "*Azure® Pipelines automatically builds and tests code projects to make them available to others. It works with just about any language or project type. Azure Pipelines combines continuous integration (CI) and continuous delivery (CD) to constantly and consistently test and build your code and ship it to any target.*".

Many companies use Azure DevOps for various processes, mostly around source code based DevOps builds and releases.  Through InRule® DevOps configuration, it is possible to start an Azure DevOps pipeline as a result of a catalog event.

With rule applications being part of projects, but not treated as source code because of InRule specific handling, such a pipeline can be used for the more specialized steps required to incorporate rule applications into other build processes.  [One such example](../devops) is offered with this release of the InRule DevOps framework, with which a pipeline can be started to run regression tests and promote a rule application between two catalogs.

---
#### Configuration

The Azure DevOps action is configurable in the InRule DevOps config file, specifying the pipeline coordinates under the section labeled with the "DevOps" moniker.  The same moniker can then be listed under the actions triggered for a catalog event, under the corresponding handler entry in the same configuration file.

Using the FilterByRuleApps configuration parameter, The sample yaml files [we make available](../devops/yaml) receive the name of the rule application that triggers the event, as an input parameter, which makes it possible to run the corresponding regression tests and promote the correct rule application without permutations of multiple yaml scripts and pipelines.

This is a [sample of minimal configuration](../config/InRuleDevOps_DevOps.config) for generating the Java JAR file for the rule application being checked in, which is **applicable for a local deployment**.  **For the DevOps app service**, the configuration follows the format in the [starter cloud config file](../config/InRule.DevOps.Runtime.Service.config.json).

````
  <add key="CatalogEvents" value="CheckinRuleApp"/>
  <add key="OnCheckinRuleApp" value="DevOps"/>

  <add key="DevOps.DevOpsOrganization" value="Contoso"/>
  <add key="DevOps.DevOpsProject" value="InRule"/>
  <add key="DevOps.DevOpsPipelineID" value="1"/>
  <add key="DevOps.DevOpsToken" value="*********************"/>
  <add key="DevOps.FilterByRuleApps" value="InvoiceSample RuleApp2"/>
````

The steps for setting a personal access token (PAT) are described [here](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate).

|Configuration Key | Comments
--- | ---
|DevOps.**DevOpsOrganization**| This value is shown in the DevOps URL following this pattern: https://dev.azure.com/**Organization**/Project
|DevOps.**DevOpsProject**| Similarly, the second component in https://dev.azure.com/Organization/**Project**.
|DevOps.**DevOpsPipelineID**| The ID of the build pipeline.  Easy to find in the URL of the edit pipeline page, like https://dev.azure.com/Organization/Project/_build?definitionId=**3**.
|DevOps.**DevOpsToken**| A personal access token (PAT) is used as an alternate password to authenticate into Azure DevOps.
|DevOps.**FilterByRuleApps**| Space separated strings corresponding to the names of the rule applications that will trigger the configured DevOps pipeline.  It may be empty for when all rule applications should trigger the pipeline.  An improvement from previous versions is that the rule application name is passed to the pipeline script as a parameter.
