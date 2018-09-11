const df = require("durable-functions");
//todo: add Personal Authentication Token
const octokit = require('@octokit/rest')({
    headers: {
        'user-agent': 'GitHubCrawlerSample'        
    }
})

module.exports = async function (context, input) {
    var organizationName = input;

    var finalResult = [];
    let page = 1;
    let result = await octokit.repos.getForOrg({
        org: organizationName,
        type: "public",
        page: page
    });
    finalResult = finalResult.concat(result.data);
    while(result.data.length !== 0){
        result = await octokit.repos.getForOrg({
            org: organizationName,
            type: "public",
            page: ++page
        });
        finalResult = finalResult.concat(result.data);
    }
    return finalResult;
};