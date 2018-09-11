const df = require("durable-functions");

module.exports = df(function* (context) {
    var organizationName = context.df.getInput();
    var repositories = yield context.df.callActivityAsync("GetAllRepositoriesForOrganization", organizationName);

    var output = [];

    for (var i = 0; i < repositories.length; i++) {
        output.push(context.df.callActivityAsync("GetOpenedIssues", repositories[i]));
    }
    const results = yield context.df.Task.all(output);
    yield context.df.callActivityAsync("SaveRepositories", results);

    return context.instanceId;
});