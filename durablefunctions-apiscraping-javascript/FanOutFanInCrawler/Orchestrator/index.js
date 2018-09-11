const df = require("durable-functions");

module.exports = df(function* (context) {
    console.log('Orchestrator');
    var organizationName = context.df.getInput();

    var repositories = yield context.df.callActivityAsync("GetAllRepositoriesForOrganization", organizationName);

    var output = [];

    for (var i = 0; i <= repositories.length; i++) {
        //output.push(context.df.callActivityAsync("GetOpenedIssues", repositories[i]));
        // console.log(repositories[i].result.name);
        console.log(repositories[i].name);
    }
    // const results = yield context.df.Task.all(output);
    // var openedIssues = results.reduce((prev, curr) => curr.result);

    // yield context.df.callActivityAsync("SaveRepositories", openedIssues);

    return context.instanceId;
});