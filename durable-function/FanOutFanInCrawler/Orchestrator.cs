using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Octokit;

namespace FanOutFanInCrawler
{
    public static class Orchestrator
    {
        // this needs to be configured when creating your Azure Function
        // with the portal:
        // https://docs.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings
        // command line:
        //
        private static Func<string> getToken = () => Environment.GetEnvironmentVariable("GitHubToken", EnvironmentVariableTarget.Process);
        private static GitHubClient github = new GitHubClient(new ProductHeaderValue("FanOutFanInCrawler")) { Credentials = new Credentials(getToken()) };
        private static CloudStorageAccount account = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process));

        /// <summary>
        /// Trigger function that will use HTTP to start an Orchestrator function
        /// </summary>
        /// <param name="req"></param>
        /// <param name="starter"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("Orchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("Orchestrator", "Nuget");

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("Orchestrator")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            // retrieves the organization name from the Orchestrator_HttpStart function
            var organizationName = context.GetInput<string>();
            // retrieves the list of repositories for an organization by invoking a separate Activity Function.
            var repositories = await context.CallActivityAsync<List<(long id, string name)>>("GetAllRepositoriesForOrganization", organizationName);

            // Creates an array of task to store the result of each functions
            var tasks = new Task<(long id, int openedIssues, string name)>[repositories.Count];
            for (int i = 0; i < repositories.Count; i++)
            {
                // Starting a `GetOpenedIssues` activity WITHOUT `async`
                // This will starts Activity Functions in parallel instead of sequentially.
                tasks[i] = context.CallActivityAsync<(long, int, string)>("GetOpenedIssues", (repositories[i]));
            }

            // Wait for all Activity Functions to complete execution
            await Task.WhenAll(tasks);

            // Retrieve the result of each Activity Function and return them in a list
            var openedIssues = tasks.Select(x => x.Result).ToList();

            // Send the list to an Activity Function to save them to Blob Storage.
            await context.CallActivityAsync("SaveRepositories", openedIssues);

            return context.InstanceId;
        }

        [FunctionName("GetAllRepositoriesForOrganization")]
        public static async Task<List<(long id, string name)>> GetAllRepositoriesForOrganization([ActivityTrigger] DurableActivityContext context)
        {
            var organizationName = context.GetInput<string>();
            var repositories = (await github.Repository.GetAllForOrg(organizationName)).Select(x => (x.Id, x.Name)).ToList();
            return repositories;
        }

        [FunctionName("GetOpenedIssues")]
        public static async Task<(long id, int openedIssues, string name)> GetOpenedIssues([ActivityTrigger] DurableActivityContext context)
        {
            var parameters = context.GetInput<(long id, string name)>();
            var issues = (await github.Issue.GetAllForRepository(parameters.id)).ToList();

            return (parameters.id, issues.Count(x => x.State == ItemState.Open), parameters.name);
        }

        [FunctionName("SaveRepositories")]
        public static async Task SaveRepositories([ActivityTrigger] DurableActivityContext context)
        {
            var parameters = context.GetInput<List<(long id, int openedIssues, string name)>>();
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference("Repositories");
            await table.CreateIfNotExistsAsync();

            var batchOperation = new TableBatchOperation();
            foreach (var parameter in parameters)
            {
                batchOperation.Add(TableOperation.InsertOrMerge(new Repository(parameter.id)
                {
                    OpenedIssues = parameter.openedIssues,
                    RepositoryName = parameter.name
                }));
            }
            await table.ExecuteBatchAsync(batchOperation);
        }

        public class Repository : TableEntity
        {
            public Repository(long id)
            {
                PartitionKey = "Default";
                RowKey = id.ToString();
            }
            public int OpenedIssues { get; set; }
            public string RepositoryName { get; set; }
        }
    }
}