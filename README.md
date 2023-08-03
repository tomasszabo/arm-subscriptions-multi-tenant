# Azure Resource Manager: Get subscriptions from multiple tenants

This Azure Function is an example how to retrieve subscriptions details from Azure Resource Manager REST API for multiple tenants and return merge data as JSON which can be later used e.g. in Power BI report.

What the functions does:

1. Authenticates to each tenant.
2. Retrieves subscriptions list from Azure Resource Manager REST API.
3. Returns merged data for all tenants.

# Prerequisites

- [Create Azure AD service principal](#create-azure-ad-service-principal)
- [Add Azure AD service principal to tenants](#add-azure-ad-service-principal-to-tenants)
- [Configured permissions](#configure-permissions)
- [Deployed and configured Azure Function App](#configure-azure-function-app)

# Create Azure AD service principal

Create a service principal in your primary tenant (e.g., where the Azure Function will be deployed). Service principal will be used to authenticate Azure Function in all tenants.

Follow these steps to create service principal in Azure AD:

1. Sign-in to the [Azure portal](https://portal.azure.com/).
2. Search for and Select `Azure Active Directory`.
3. Select `App registrations`, then select `New registration`.
4. Name the application, for example `example-app`.
5. Select account type `Accounts in any organizational directory (Any Azure AD directory - Multitenant)`.
6. Select `Register`.

You've created your Azure AD application and service principal. Note down the application (client) id (will be used later).

Now create client secret:

1. Select `Certificates & secrets`, then select `Client secrets` and `New client secret`
2. Enter secret description, for example `function-secret` and select `Add`.
3. Note down the `Value` of newly created secret (it will be not visible anymore after you leave this screen).

# Add Azure AD service principal to tenants

In previous step Azure AD app registration and service principal was created in primary tenant. Additional service principal to app registration must be created in order to allow authentication for Azure Function to other tenants. This service principal must be created in tenant where Azure Function should have access.

Follow these steps to add service principal to other tenants:

1. Using Azure CLI, login to tenant where service principal should be added:

```bash
az login --tenant "{tenantId}"
```

2. Create service principal for app registration from primary tenant:

```bash
az ad sp create --id "{applicationId}" \
--query "{servicePrincipalId:id,appId:appId,displayName:displayName}"
```

Response example is:

```json
{
	"appId": "4c3e3be1-b735-41b1-a842-f095b9a45849",
	"displayName": "App in primary tenant",
	"servicePrincipalId": "2ae09b6c-6b2d-4ce0-984c-d52eb3a9a406"
}
```

3. Note down the service principal id (will be used later).

Run above steps for each tenant where Azure Function should have access.

# Configure permissions in tenants

Access to subscriptions within tenants must be granted to allow Azure Function read subscriptions data. Access is granted adding by assigning service principal to Reader role on subscription level.

Follow these steps to assign service principal to Reader role on subscription level:

1. Using Azure CLI, login to tenant where service principal should be assigned to Reader role:

```bash
az login --tenant "{tenantId}"
```

2. Create assignment:

```bash
az role assignment create --assignee "{servicePrincipalId}" \
--role "Reader" \
--scope "/subscriptions/{subscriptionNameOrId}"
```

Run above steps for each tenant where Azure Function should have access.

# Configure Azure Function App

## Create Azure Function App

1. Using Azure CLI, login to tenant where Azure Function App should be created:

```bash
az login --tenant "{tenantId}"
```

2. Create a resource group 

```bash
az group create --name "{resourceGroup}" --location "{location}"
```

3. Deploy Azure Function App from Bicep template located in repository

```bash
az deployment group create --resource-group {resourceGroup} \
--template-file ./bicep/main.bicep \
--parameters location='{location}' prefix='{resourcesPrefix}' \
--query {functionAppName:properties.outputs.functionAppName.value}
```

4. Note down the Azure Function App name (will be used later).


## Deploy Azure Function

1. Create a deployment zip file in the repository directory with following commands:

```bash
dotnet publish -o ./artifacts 
cd artifacts
zip -r -X arcifacts.zip *
```

2. Deploy zip file to Azure Function App

```bash
az functionapp deployment source config-zip \
-g {resourceGroup} \
-n {functionAppName} \
--src {zipFilePath}
```

## Configure Azure Function App

1. Sign-in to the [Azure portal](https://portal.azure.com/).
2. Search for and Select given Azure Function App.
3. Select `App registrations`, then select `New application setting`.
4. Add following application settings:

|Name|Value|
|--|--|
|TenantIds|Comma-separated list of tenant ids|
|ClientId|Application (client) id created in [Create Azure AD service principal](#create-azure-ad-service-principal) step|
|ClientSecret|Client secret created in [Create Azure AD service principal](#create-azure-ad-service-principal) step|

5. Save your changes

## Run Azure Function

1. Sign-in to the [Azure portal](https://portal.azure.com/).
2. Search for and Select given Azure Function App.
3. Select `Functions`, then select deployed Azure Function.
4. Select `Get Function Url` and copy the Azure Function (including function key)
5. Paste url in browser and the Azure Function should return subscriptions data in JSON format. Azure Function can be used e.g. as data source for Power BI report.

## Restrict access to Azure Function App (optional)

By default Azure Function is publicly available and can be called by anyone who has the function key. It is recommended to secure access to Azure Function e.g. with access restrictions and only allow calls e.g. from PowerBI service (which is using Azure Function to generate report).

Following steps will restrict access to Azure Function:

1. Sign-in to the [Azure portal](https://portal.azure.com/).
2. Search for and Select given Azure Function App.
3. Select `Functions`, then select `Networking`.
4. Select `Access Restrictions`, then select `Add`.
5. Define new rule with following values and add it:

|Field|Value|
|--|--|
|Name|AllowPowerBIService|
|Priority|100|
|Type|Service Tag|
|Service Tag|PowerBI|

6. Change `Unmatched rule action` to `Deny`.
7. Save your changes.

Now Azure Function app will be available only for requests from Power BI service.

# Resources

[List subscriptions - Azure Resource Manager REST API](https://learn.microsoft.com/en-us/rest/api/resources/subscriptions/list?tabs=HTTP)


# LICENSE

Distributed under MIT License. See [LICENSE](LICENSE) for more details.