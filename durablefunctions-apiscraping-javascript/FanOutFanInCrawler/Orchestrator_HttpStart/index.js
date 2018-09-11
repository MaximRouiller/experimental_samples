const uuid = require('uuid/v4');

module.exports = function (context, req, starter) {
    var id = uuid();
    console.log(id);
    context.bindings.starter = [{
        FunctionName: "Orchestrator",
        Input: "Nuget",
        InstanceId: id
    }];
    
    context.done(null);
};