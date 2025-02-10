### Save a Rule Application to a Repository

After a checkin, this allows a rule application to be uploaded to a specified external repository for backup purposes.

---
#### Configuration

In order to push the Rule Application to a repository, at least one repository source must be configured (GitHub, Azure Git, or Box).


```
  <appSettings>
    <add key="CatalogUsername" value="admin"/>
    <add key="CatalogPassword" value="********"/>

    <add key="CatalogEvents" value="CheckinRuleApp"/>
    <add key="OnCheckinRuleApp" value="SaveToRepo"/>
    <add key="SaveToRepo.UploadTo" value="AzureDevOpsGit"/>
    <add key="SaveToRepo.NotificationChannel" value="Slack"/>
    
    <add key="AzureDevOpsGit..."/>

    <add key="Slack...."/>
  </appSettings>
```
You may also need to add an assembly binding redirect
```
  <dependentAssembly>
    <assemblyIdentity name="System.Net.Http.Formatting" publicKeyToken="31bf3856ad364e35" culture="neutral" />
    <bindingRedirect oldVersion="0.0.0.0-6.0.0.0" newVersion="6.0.0.0" />
  </dependentAssembly>
```

|Configuration Key | Comments
--- | ---
|SaveToRepo.**UploadTo**| A single moniker or a space separated list of monikers for the configuration sections for where the rule application will be uploaded.  Choices are: GitHub, AzureDevOpsGit and Box (for Box.com).
|SaveToRepo.**NotificationChannel**| For the notification channels receiving a link to the location where the Rule Application is uploaded.
