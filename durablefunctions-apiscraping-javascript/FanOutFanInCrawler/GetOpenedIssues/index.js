const df = require("durable-functions");
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
    console.log('GetOpenedIssues');
    let page = 1
    let issueCount = 0;

    do {
        var result = await octokit.issues.getForRepo({
            owner: input.owner.login,
            repo: input.name,
            state: 'open',
            per_page: 100,
            page: page
        });
        page++;
        issueCount += result.data.length;
    }
    while (result.data.length !== 0)

    return {id: input.id, openedIssues: issueCount, name: input.name };
};