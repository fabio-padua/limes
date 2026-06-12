targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the azd environment — drives resource names and tags.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('Location for the Azure AI Foundry account and model deployment. Defaults to the primary location.')
param aiLocation string = ''

@description('Chat model deployed in Foundry and used by the agents.')
param chatModelName string = 'gpt-5.2'

@description('Chat model version.')
param chatModelVersion string = '2025-12-11'

@description('Model deployment SKU (GlobalStandard for the GPT-5 family).')
param chatModelSkuName string = 'GlobalStandard'

@description('Model deployment capacity in thousands of tokens per minute. String so it can be overridden via an azd env var; converted to int before use.')
param chatModelCapacity string = '50'

@description('Object ID of the user/service principal granted data-plane access for local runs. azd sets AZURE_PRINCIPAL_ID. Leave empty to skip.')
param principalId string = ''

@description('Container image for the orchestrator job. Left empty on first provision; azd sets it on deploy.')
param orchestratorImage string = ''

@description('Container image for the Limes.Web app. Leave empty on first provision (a placeholder image is used); azd deploy builds and pushes the real image and updates the Container App separately.')
param webImage string = ''

var tags = { 'azd-env-name': environmentName }
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'limes-resources'
  scope: rg
  params: {
    location: location
    aiLocation: empty(aiLocation) ? location : aiLocation
    tags: tags
    resourceToken: resourceToken
    chatModelName: chatModelName
    chatModelVersion: chatModelVersion
    chatModelSkuName: chatModelSkuName
    chatModelCapacity: int(chatModelCapacity)
    principalId: principalId
    orchestratorImage: orchestratorImage
    webImage: webImage
  }
}

// azd reads these env-prefixed outputs back into the environment (.azure/<env>/.env).
output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.containerRegistryLoginServer
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.containerRegistryName
output AZURE_STORAGE_ACCOUNT string = resources.outputs.storageAccountName

output LIMES_FOUNDRY_ENDPOINT string = resources.outputs.foundryEndpoint
output LIMES_FOUNDRY_DEPLOYMENT string = resources.outputs.chatDeploymentName
output LIMES_INTAKE_CONTAINER_URL string = resources.outputs.intakeContainerUrl
output LIMES_REPORTS_CONTAINER_URL string = resources.outputs.reportsContainerUrl
output LIMES_JOB_NAME string = resources.outputs.jobName
output LIMES_WEB_URL string = resources.outputs.webUrl
