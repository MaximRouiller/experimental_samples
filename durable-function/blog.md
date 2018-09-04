# Introduction to Azure Durable Functions

Azure Durable Functions is a new programming model based on Microsoft serverless' platform Azure Functions. It allows you to write a workflow as code and have the execution run with the scalability and the reliability of serverless with high throughput.

## Scenario

Initially, I wanted to index data from GitHub repositories. I explained it all in a [previous post](https://blogs.msdn.microsoft.com/appserviceteam/2018/08/06/learn-how-to-orchestrate-serverless-functions-by-scraping-apis-in-8-minutes/?WT.mc_id=dotnet-blog-marouill), but I'll drill down into the concepts today.

To summarize the post, I wanted to retrieve a list of repositories that I wanted to index. Then, query each repositories for more detailed information. Finally, save the data into a database.

Today, we'll do something similar but to keep things simple, I'll save the content to Azure Blob Storage.

If you want to follow along, I've created [a sample][samplelink] that contains all the code in this post.

Let's get started.

## Concepts

Besides the common [serverless concepts](https://docs.microsoft.com/dotnet/standard/serverless-architecture/?WT.mc_id=dotnet-blog-marouill), Azure Durable Functions introduces more concepts that make developing complex workflow easy.

Let's start with the basic concepts and work our way up.

### Definition of a Function

A serverless function on Azure is a unit of work that is, in C#, a single method from a static class. Azure Functions allows you only pay for what you use as well as scaling that piece of code for you.

Generous [free quotas](https://azure.microsoft.com/pricing/details/functions/?WT.mc_id=dotnet-blog-marouill) are also given to allow you to experiment quickly and cheaply in the cloud.

In C#, we can write a simple Function with the following code.

```csharp
public static class MyFunction
{
    [FunctionName("MyFirstFunction")]
    public void MyFirstFunction()
    {
        //...
    }
}
```

### Definition of a trigger

A trigger is an event happening to which a piece of code responds.

It is part of what is called a *binding* which hooks a method, a variable, or a parameter of your code to external systems.

Bindings come in 3 types, but we're only interested in the latter. For more information on bindings, you can read the [concept page](https://docs.microsoft.com/azure/azure-functions/functions-triggers-bindings?WT.mc_id=dotnet-blog-marouill)

* Input
* Output
* Trigger

By looking at the list of [supported bindings](https://docs.microsoft.com/azure/azure-functions/functions-triggers-bindings?WT.mc_id=dotnet-blog-marouill#supported-bindings), we see a `Trigger` column. Triggers include storage events (Blob Storage, Cosmos DB, or other.) as well as eventing systems such as Event Grid and Event Hubs.

The most common triggers, however, are `Http`, which respond to an HTTP request from a user, as well as `Timer` which allows you to specific code on a specified schedule.

Those triggers are applied directly on a parameter of a function.

```csharp
public static class MyFunction
{
    // The timer trigger is based on a CRON expression.
    // {second} {minute} {hour} {day} {month} {day-of-week}
    // In this case, the timer is every 5 minutes.
    [FunctionName("MyTimerTrigger")]
    public static void MyTimerTrigger([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer) { /* ... */ }

    [FunctionName("HttpTriggerCSharp")]
    public static async Task<HttpResponseMessage> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req) { /* ... */}
}
```

Those are traditional functions that you find in a serverless application deployed on Azure. They are simple and respond to events.

What if you need something more complicated? What about calling functions with functions? For example, invoking one function for every element of a list then reconciling the result and saving it to a file.

Writing a complex workflow with simple code is where Durable Functions comes in.

### Definition of an Orchestrator Function

An Orchestrator is an Azure Function with specific behaviors attached to them. As an example, it automatically set checkpoint during its execution, shut itself down while waiting for other functions to finish executing, then replay through the checkpoint to resume execution.

They are at the very heart of how Durable Functions works.

It is important to remember how an Orchestrator function operates differently than a standard function. While a normal function only executes once per event, the orchestrator is restarted many times while waiting for other functions to complete.

This kind of behavior means that this function needs to be *deterministic*. It must return the same result each time. It is crucial then to not use `DateTime.Now`, `Guid.NewGuid()` or anything generating a different result in this method. [More information is available on the checkpoint and replay pattern that Orchestrators are using internally](https://docs.microsoft.com/azure/azure-functions/durable-functions-checkpointing-and-replay?WT.mc_id=dotnet-blog-marouill).

An Orchestrator function is a function with an `OrchestrationTrigger` on a `DurableOrchestrationContext` object parameter.

```csharp
[FunctionName("Orchestrator")]
public static async Task<string> RunOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context)
{
    return context.InstanceId;
}
```

It is invoked by a `DurableOrchestrationClient` decorated with an `OrchestrationClientAttribute` injected within an ordinary function with any triggers. Let's take a `Timer` trigger as an example.

```csharp
[FunctionName("MyTimerTrigger")]
public static void MyTimerTrigger([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, [OrchestrationClient]DurableOrchestrationClient starter)
{
    // Function input comes from the request content.
    string instanceId = await starter.StartNewAsync("Orchestrator", null);
    /* ... */
}
```

At this point, we have an orchestrator that is ready to run other functions.

### Definition of an Activity Function

Not every function can be used with an orchestrator. Those functions become an Activity Function when an `ActivityTriggerAttribute` is on a `DurableActivityContext` parameter.

```csharp

[FunctionName("MyActivity")]
public static async Task MyActivity([ActivityTrigger] DurableActivityContext context)
{
    /* ... */
}
```

Activities are part of the checkpoint and replay pattern described previously. Among the differences with other functions, they do not respond to other triggers than an orchestrator calling them.

An activity is a perfect place to have code accessing a database, sending emails, or any other external systems. Activities that have been called and returned have their results cached. Meaning, that returning 1000 rows from a database won't be done multiple times but only a single time.

As an example, our sample uses an activity function to retrieve the list of repositories from which we want to extract more data.

```csharp
[FunctionName("GetAllRepositoriesForOrganization")]
public static async Task<List<(long id, string name)>> GetAllRepositoriesForOrganization([ActivityTrigger] DurableActivityContext context)
{
    // retrieves the organization name from the Orchestrator function
    var organizationName = context.GetInput<string>();
    // invoke the API to retrieve the list of repositories of a specific organization
    var repositories = (await github.Repository.GetAllForOrg(organizationName)).Select(x => (x.Id, x.Name)).ToList();
    return repositories;
}
```

An Activity Function is not re-executed for the same Orchestrator instance. In this scenario, we preserve the list of repositories without invoking the API again.

This caching mechanism gives us significant leverage while orchestrating complex workflows. First, it allows us to keep the list of data on which we operate fixed. Second, since the orchestrator may call that function multiple times, it's essential to have the result cached to avoid crashing external systems.  Finally, it becomes possible to debug what we send/return to activities since every execution is logged transactionally by the Durable Functions component.

### Code as orchestration

With those concepts clarified, let's look at Code as Orchestration.

Workflows in the past were traditionally built with either a graphic designer or data persistence format (XML, JSON, YAML, or other). Those are easy to create and understand for small workflow. However, they become tough to debug and understand where a failure occurs.

On top of that, they were never meant to run on a high scale infrastructure where one part of the workflow would need to be scale differently.

Let's take a simple example.

> I want to retrieve the list of opened issues for all repositories within a GitHub organization and save the count somewhere in the cloud.

Here's how it looks in pseudo-code.

```none
repositories = getAllRepositories(organizationName);
foreach( repository in repositories)
    repository.getOpenedIssuesCount()
cloud.save(repository)
```

Here's how you would represent it with Azure Durable Functions.

It is the same code found in [the sample][samplelink] on GitHub.

```csharp
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
```

Let's walk through this orchestrator line by line. I retrieve an organization name from the function that triggered the Orchestrator. The input mechanism allows me to reuse the code for more than a single purpose. Then, I invoke a function that returns me a list of all repositories.

Once the list is returned, and not before, we invoke as many `GetOpenedIssues` functions as necessary to complete our task. Note that we are not using the `await` keyword here meaning that they are executing in parallel instead of sequentially.

Finally, we wait for all the functions to complete their executions before sending the compiled results to a `SaveRepositories` function to save them to Blob Storage.

With as few as 8 lines of codes, we managed to build a complex workflow without XML/JSON that your team can understand quickly.

Give it a try now by downloading [the sample][samplelink] and trying it out! If you want to run it in the cloud, [get a free Azure subscription][freeazure] and take advantage of free Azure Functions.

## Resources

To get more information on Azure Durable Functions here's some reading material.

* EBook: [Serverless apps: Architecture, patterns and Azure implementation](https://docs.microsoft.com/dotnet/standard/serverless-architecture/?WT.mc_id=dotnet-blog-marouill)
* [Learn how to orchestrate serverless function by scraping APIs in 8 minutes](https://blogs.msdn.microsoft.com/appserviceteam/2018/08/06/learn-how-to-orchestrate-serverless-functions-by-scraping-apis-in-8-minutes/?WT.mc_id=dotnet-blog-marouill)
* [Durable Function Orchestrator Checkpoint and replay](https://docs.microsoft.com/azure/azure-functions/durable-functions-checkpointing-and-replay?WT.mc_id=dotnet-blog-marouill)

[freeazure]: azure.microsoft.com/free/?WT.mc_id=dotnet-blog-marouill
[samplelink]: https://aka.ms/DurableFunctionsSample
<!-- https://github.com/MaximRouiller/experimental_samples/tree/master/durable-function  -->
<!-- https://github.com/Azure-Samples/durablefunctions-apiscraping-dotnet-->