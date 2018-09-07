const df = require("durable-functions");
const octokit = require('@octokit/rest')({
    headers: {
        'user-agent': 'GitHubCrawlerSample'
    }
})

module.exports = df(function* (context) {
    // var organizationName = context.df.getInput();
    // let result = octokit.repos.getForOrg({
    //     org: organizationName,
    //     type: "public",
    //     page: 100
    // });

    // return result;
});