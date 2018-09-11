const df = require("durable-functions");
var storage = require('azure-storage');

module.exports = async function (context, input) {
    var tableService = storage.createTableService(process.env['AzureWebJobsStorage']);
    tableService.createTableIfNotExists('Repositories', function (error) {
        if (error) {
            console.error(error);
        }
        if (!error) {
            var batch = new storage.TableBatch();
            for (var i = 0; i < input.length; i++) {
                var repository = input[i];
                batch.insertOrReplaceEntity({
                    PartitionKey: {'_': 'Default'},
                    RowKey: {'_': repository.id.toString()},
                    OpenedIssues: {'_': repository.openedIssues},
                    RepositoryName: {'_': repository.name}
                });
            }
            tableService.executeBatch('Repositories', batch, function (error, result, response) {
                if (error) {
                    console.error(error);
                }
            });
        }
    });




};