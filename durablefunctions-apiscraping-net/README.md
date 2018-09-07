---
services: functions
platforms: dotnet
author: MaximRouiller
---

# Retrieve opened issue count on GitHub with Azure Durable Functions

## Build

The project can be built with the latest version of the [.NET CLI](https://www.microsoft.com/net/download?WT.mc_id=dotnet-sample-marouill). Or directly within Visual Studio 2017 with the .NET Core SDK.

```bash
dotnet build
```

## Running the Sample

### Pre-requisite

* GitHub Personal Access Token
  * [How to create a Personal Access Token](https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/)
* [Azure Functions Tooling](https://docs.microsoft.com/azure/azure-functions/functions-develop-vs?WT.mc_id=dotnet-sample-marouill)
* Visual Studio 2017
* [.NET Core 2.1 SDK](https://www.microsoft.com/net/download?WT.mc_id=dotnet-sample-marouill)
* Azure Subscription (if running on Azure)
  * [Get a free Azure Trial Subscription](https://azure.microsoft.com/free/?WT.mc_id=dotnet-sample-marouill)

### Locally

Open the solution in Visual Studio 2017. Change the `GitHubToken` value in `local.settings.json` to match your GitHub Personal Access Token created previously in the pre-requisite.

### On Azure

First, you will need to provision the service. Look into the `provision.ps1` file provided and modify the name of the storage and Azure Web Site values to ensure that they are unique.

Then you can execute the file with the previously generated GitHub.

```powershell
.\provision.ps1 -githubToken <TOKEN> -resourceGroup <ResourceGroupName> -storageName <StorageAccountName> -functionName <FunctionName>
```

# Resources

* [Azure Functions Documentation](https://docs.microsoft.com/azure/azure-functions/?WT.mc_id=dotnet-sample-marouill)
* [Introduction to Azure Functions](https://docs.microsoft.com/azure/azure-functions/functions-overview?WT.mc_id=dotnet-sample-marouill)
* [Durable Functions overview](https://docs.microsoft.com/azure/azure-functions/durable-functions-overview?WT.mc_id=dotnet-sample-marouill)
* Durable Functions pattern used in this sample
  * [Chaining](https://docs.microsoft.com/azure/azure-functions/durable-functions-sequence?WT.mc_id=dotnet-sample-marouill)
  * [Fan-out/fan-in](https://docs.microsoft.com/azure/azure-functions/durable-functions-cloud-backup?WT.mc_id=dotnet-sample-marouill)