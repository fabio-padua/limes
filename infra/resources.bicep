@description('Location for most resources.')
param location string

@description('Location for the Azure AI Foundry account and model deployment.')
param aiLocation string

@description('Tags applied to every resource.')
param tags object

@description('Stable token used to build globally-unique resource names.')
param resourceToken string

param chatModelName string
param chatModelVersion string
param chatModelSkuName string
param chatModelCapacity int

@description('Object ID granted data-plane roles for local runs. Empty to skip.')
param principalId string

@description('Orchestrator container image. Empty uses a placeholder until azd deploys the real one.')
param orchestratorImage string

@description('Web (Limes.Web) container image. When empty, a placeholder image is used; azd deploy later builds, pushes, and updates the Container App with the real image.')
param webImage string

// Built-in role definition IDs.
var openAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' // Cognitive Services OpenAI User
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull

var placeholderImage = 'mcr.microsoft.com/k8se/quickstart-jobs:latest'
var jobImage = empty(orchestratorImage) ? placeholderImage : orchestratorImage
// Both the placeholder (MCR quickstart) and the real Limes.Web image listen on 80
// (the Dockerfile sets ASPNETCORE_HTTP_PORTS=80), matching ingress targetPort: 80, so the
// first-provision revision is healthy before azd deploys the real image.
var webPlaceholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
var webImageResolved = empty(webImage) ? webPlaceholderImage : webImage

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${resourceToken}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${resourceToken}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource uami 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${resourceToken}'
  location: location
  tags: tags
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'acr${resourceToken}'
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

resource ai 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: 'ai-${resourceToken}'
  location: aiLocation
  tags: tags
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    customSubDomainName: 'ai-${resourceToken}'
    publicNetworkAccess: 'Enabled'
    // Force Entra ID auth — DefaultAzureCredential, no API keys.
    disableLocalAuth: true
  }
}

resource chat 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: ai
  name: chatModelName
  sku: {
    name: chatModelSkuName
    capacity: chatModelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: chatModelName
      version: chatModelVersion
    }
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${resourceToken}'
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    // Force Entra ID auth for blob access — no account keys.
    allowSharedKeyAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource intakeContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'intake'
}

resource reportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'reports'
}

resource caeEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${resourceToken}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource job 'Microsoft.App/jobs@2024-03-01' = {
  name: 'job-${resourceToken}'
  location: location
  // azd matches this tag to build/push the image and update the job on deploy.
  tags: union(tags, { 'azd-service-name': 'orchestrator' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uami.id}': {}
    }
  }
  properties: {
    environmentId: caeEnv.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: uami.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'orchestrator'
          image: jobImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'LIMES_MODE', value: 'agents' }
            { name: 'LIMES_FOUNDRY_ENDPOINT', value: ai.properties.endpoint }
            { name: 'LIMES_FOUNDRY_DEPLOYMENT', value: chat.name }
            { name: 'LIMES_INTAKE', value: '${storage.properties.primaryEndpoints.blob}intake/sample-intake.json' }
            { name: 'LIMES_OUTPUT', value: '${storage.properties.primaryEndpoints.blob}reports' }
            { name: 'LIMES_KNOWLEDGE', value: '/app/knowledge/ai-coe-knowledge.md' }
            // Tell DefaultAzureCredential which user-assigned identity to use.
            { name: 'AZURE_CLIENT_ID', value: uami.properties.clientId }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
          ]
        }
      ]
    }
  }
}

// --- Limes.Web: long-lived Container App (browser UI + API) ---

resource webApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-web-${resourceToken}'
  location: location
  // azd matches this tag to build/push the image and update the app on deploy.
  tags: union(tags, { 'azd-service-name': 'web' })
  // Dedicated system-assigned identity with ONLY AcrPull (granted below) — the public web
  // app must not inherit the orchestrator identity's OpenAI/Blob data-plane roles.
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: caeEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        // Port 80 matches both the placeholder image and the real image (which sets
        // ASPNETCORE_HTTP_PORTS=80), so the first revision is healthy on initial provision.
        targetPort: 80
        transport: 'auto'
      }
      registries: [
        {
          server: acr.properties.loginServer
          // 'system' tells Container Apps to authenticate ACR pulls with the
          // system-assigned identity (see the AcrPull assignment below).
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImageResolved
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            // Limes.Web runs the deterministic pipeline in-process — no Foundry/Blob needed.
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
          ]
        }
      ]
      // Keep one warm replica for snappy demos; scale up under load.
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// The web app's system-assigned identity needs AcrPull to fetch its image from the private
// registry. The first-provision placeholder is a public MCR image (no ACR auth), so this can
// be assigned after the app exists without a chicken-and-egg on initial provision.
resource webAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, webApp.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Role assignments: managed identity (job) ---

resource jobOpenAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(ai.id, uami.id, openAiUserRoleId)
  scope: ai
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', openAiUserRoleId)
    principalId: uami.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource jobIntakeBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(intakeContainer.id, uami.id, blobDataContributorRoleId)
  scope: intakeContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: uami.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource jobReportsBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(reportsContainer.id, uami.id, blobDataContributorRoleId)
  scope: reportsContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: uami.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource jobAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, uami.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: uami.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Role assignments: developer principal (for local az-login runs) ---

resource userOpenAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(ai.id, principalId, openAiUserRoleId)
  scope: ai
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', openAiUserRoleId)
    principalId: principalId
  }
}

resource userIntakeBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(intakeContainer.id, principalId, blobDataContributorRoleId)
  scope: intakeContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: principalId
  }
}

resource userReportsBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(reportsContainer.id, principalId, blobDataContributorRoleId)
  scope: reportsContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: principalId
  }
}

output containerRegistryLoginServer string = acr.properties.loginServer
output containerRegistryName string = acr.name
output storageAccountName string = storage.name
output foundryEndpoint string = ai.properties.endpoint
output chatDeploymentName string = chat.name
output intakeContainerUrl string = '${storage.properties.primaryEndpoints.blob}intake'
output reportsContainerUrl string = '${storage.properties.primaryEndpoints.blob}reports'
output jobName string = job.name
output webUrl string = 'https://${webApp.properties.configuration.ingress.fqdn}'
