const octokit = require('@octokit/rest')({
    headers: {
        'user-agent': 'FanOutFanInCrawler'
    }
});
octokit.authenticate({
    type: 'token',
    token: process.env["GitHubToken"]
});

module.exports = async function (context, input) {
    var organizationName = input;

    var finalResult = [];
    let page = 1;
    do {
        var result = await octokit.repos.getForOrg({
            org: organizationName,
            type: "public",
            page: page
        });
        page++;
        finalResult = finalResult.concat(result.data);
    }
    while (result.data.length !== 0)
    return finalResult;
};