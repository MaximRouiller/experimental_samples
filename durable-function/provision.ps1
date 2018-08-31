param (
    # How to create a token here: https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/
    [string]$githubToken
)

# Creates a Resource group to hold our application
az group create -l eastus --n FunctionCrawler

# Storage account where Azure Functions will keep its data and logs
az storage account create --sku Standard_LRS --location EastUS --kind Storage -g FunctionCrawler -n functioncrawlerstorage

# Creates our Function Application
az functionapp create -n FanOutFanInCrawler -g FunctionCrawler -s functioncrawlerstorage -c eastus

# Configures Functions Application Settings
az functionapp config appsettings set -g FunctionCrawler -n FanOutFanInCrawler --settings GitHubToken=$githubToken