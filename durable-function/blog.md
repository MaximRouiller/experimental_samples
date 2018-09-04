    # Introduction to Azure Durable Functions for .NET Core Developers

    Azure Durable Functions is a new programming model based on Microsoft serverless' platform Azure Functions. It allows you to write a workflow as code and have the execution run with the scalability and the reliability of serverless with high throughput.

    As a .NET Core developer, Azure Functions v2 gives you the power to run .NET Core code with all the benefits that C# can bring to your code.

    ## Concepts

    Beside the normal [serverless concepts](https://docs.microsoft.com/dotnet/standard/serverless-architecture/?WT.mc_id=dotnet-blog-marouill) that you ought to know, Azure Durable Functions introduces more concepts that makes developing complex workflow easy.

    If you need to know what .NET Core is all about, we have [great tutorials](https://docs.microsoft.com/dotnet/core/tutorials/?WT.mc_id=dotnet-blog-marouill) to get you started.

    Let's start with the basic concepts and work our way up.

    ### Definition of a Function

    A serverless function on Azure is a unit of work that is represented as a single method in C#. Azure Functions allows you only pay for what you use as well as scaling that piece of code for you.

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

    A trigger is an event happening to which the code will respond.

    A trigger is part of what is called a *binding* which hook a method, a variable, or a parameter of your code to external systems.

    Bindings comes in 3 types but we're only interested in the latter. For more information on bindings, you can read the [concept page](https://docs.microsoft.com/azure/azure-functions/functions-triggers-bindings?WT.mc_id=dotnet-blog-marouill)

    * Input
    * Output
    * Trigger

    By looking at the list of [supported bindings](https://docs.microsoft.com/azure/azure-functions/functions-triggers-bindings?WT.mc_id=dotnet-blog-marouill#supported-bindings), we see a `Trigger` column. Triggers includes storage events (Blob Storage, Cosmos Db, etc.) as well as eventing system such as Event Grid and Event Hubs. 

    The most common triggers however are `Http`, which respond to an HTTP request from a user, as well as `Timer` which allows you to specific code on a specified schedule.

    Those triggers are applied directly on a parameter of a function.

    ```csharp

    public static class MyFunction
    {
        [FunctionName("MyTimerTrigger")]
        public static void MyTimerTrigger([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer) { /* ... */ }

        [FunctionName("HttpTriggerCSharp")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req) { /* ... */}
    }
    ```

    Those are traditional functions that you will find in a serverless application deployed on Azure. They are simple and respond to events.

    What if you need something more complicated? What about calling functions with functions?

    This is where Durable Functions comes in.

    ### Definition of an Orchestrator Function

    An Orchestrator function is seen exactly as a standard Function but the way it is executed by the Azure Functions runtime gives it different properties. They are at the very heart of how Durable Functions works.

    It is important to remember that an Orchestrator function is a special function that operates differently than a normal function. While a normal function will only execute once per event, the orchestrator will be executed many times to keep track of the other functions it is starting.

    This means that this functions needs to be *deterministic*. This means, it must return exactly the same result each time. It is very important then to not use `DateTime.Now`, `Guid.NewGuid()` or anything generating different result in this method. [More information is available on the checkpoint and replay pattern that Orchestrators are using internally](https://docs.microsoft.com/azure/azure-functions/durable-functions-checkpointing-and-replay?WT.mc_id=dotnet-blog-marouill).

    An Orchestrator function is represented with the function an `OrchestrationTrigger` on a `DurableOrchestrationContext` object parameter.

    ```csharp
    [FunctionName("Orchestrator")]
    public static async Task<string> RunOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context)
    {
        return context.InstanceId;
    }
    ```

    It is invoked by a `DurableOrchestrationClient` decorated with an `OrchestrationClientAttribute` injected within a normal functions with any triggers. Let's take an `Timer` trigger as an example.

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

    Not every function can be used with an orchestrator. Those functions needs to be indicated as such with an `ActivityTrigger` on a `DurableActivityContext` parameter.

    ```csharp

    [FunctionName("MyActivity")]
    public static async Task MyActivity([ActivityTrigger] DurableActivityContext context)
    {
        /* ... */
    }
    ```

    An activity is part of the checkpoint and replay pattern described previously. Among the differences with other functions, they do not respond to other triggers than an orchestrator calling them.

    An activity is the perfect place to have code accessing a database, sending emails, or any other external systems. Activities that have been called and returned will have their results saved. This means that returning 1000 rows from a database won't be done multiple times but only a single time.

    This gives us a lot of leverage while orchestrating complex workflows. First, it allows us to keep the list of data on which we operate static. Second, since the orchestrator may call that function multiple times, it's important to have the result cache to avoid crashing external systems. Finally, it becomes possible to debug what was sent/returned by activities since everything that is done is logged by the Durable Functions component.

    ### Code as orchestration

    With those concepts clarified, let's look at code as orchestration.

    Workflows in the past were traditionally built with either a graphic designer or data persistence format (XML, JSON, YAML, etc.). Those are easy to create and understand for small workflow. However, they become extremely hard to debug and understand where failure occurs.

    On top of that, they were never meant to be run on a high scale infrastructure where one part of the workflow would need to be scaled.

    Let's take a simple example.

    > I want to retrieve the list of opened issues for all repositories within a GitHub organization and save the count somewhere in the cloud.

    Here's how it would be represented in pseudo-code.

    ```none
    repositories = getAllRepositories(organizationName);
    foreach( repository in repositories)
        repository.getOpenedIssuesCount()
    cloud.save(repository)
    ```

    And here's how you would represent it with Azure Durable Functions.

    if you want to follow along and see all the code, you can [try it yourself][samplelink] on GitHub.


    ```csharp
    [FunctionName("Orchestrator")]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] DurableOrchestrationContext context)
    {
        var organizationName = context.GetInput<string>();
        var repositories = await context.CallActivityAsync<List<(long id, string name)>>("GetAllRepositoriesForOrganization", organizationName);

        var tasks = new Task<(long id, int openedIssues, string name)>[repositories.Count];
        for (int i = 0; i < repositories.Count; i++)
        {
            tasks[i] = context.CallActivityAsync<(long, int, string)>("GetOpenedIssues", (repositories[i]));
        }
        await Task.WhenAll(tasks);

        var openedIssues = tasks.Select(x => x.Result).ToList();
        await context.CallActivityAsync("SaveRepositories", openedIssues);

        return context.InstanceId;
    }
    ```

    Let's walk through this orchestrator line by line. I retrieve an organization name from the function that triggered the Orchestrator. This allows me to reuse the code for more than a single purpose. Then, I invoke a function that will return me the list of all repositories.

    Once the list has been returned, and not before, we invoke as many `GetOpenedIssues` functions as necessary to complete our task. Note that we are not using the `await` keyword here meaning that we are not waiting on those functions to complete before we start the next one.

    Finally, we wait for all the functions to complete their executions before sending the compiled results to a `SaveRepositories` function. That function will persist the result to the cloud.

    ## Resources

    To get more information on Durable Functions, here's some reading material.

    * EBook: [Serverless apps: Architecture, patterns and Azure implementation](https://docs.microsoft.com/dotnet/standard/serverless-architecture/?WT.mc_id=dotnet-blog-marouill)
    * [Learn how to orchestrate serverless function by scraping APIs in 8 minutes](https://blogs.msdn.microsoft.com/appserviceteam/2018/08/06/learn-how-to-orchestrate-serverless-functions-by-scraping-apis-in-8-minutes/?WT.mc_id=dotnet-blog-marouill)
    * [Durable Function Orchestrator Checkpoint and replay](https://docs.microsoft.com/azure/azure-functions/durable-functions-checkpointing-and-replay?WT.mc_id=dotnet-blog-marouill)

    [samplelink]: https://aka.ms/DurableFunctionsSample
    <!-- https://github.com/MaximRouiller/experimental_samples/tree/master/durable-function  -->
    <!-- https://github.com/Azure-Samples/durablefunctions-apiscraping-dotnet-->