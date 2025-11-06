param appName string
param location string
param vnetSubnetId string
param appInsightsInstrumentationKey string
param appInsightsConnectionString string
param storageConnectionString string
param cosmosEndpoint string
param cosmosPrimaryKey string
param cosmosDatabaseName string
param cosmosContainerName string
param eventHubNamespaceId string
param eventHubName string

resource appServicePlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'S1'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2021-03-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    virtualNetworkSubnetId: vnetSubnetId
    httpsOnly: true
    siteConfig: {
      vnetPrivatePortsCount: 2
      webSocketsEnabled: true
      linuxFxVersion: 'DOTNETCORE|9.0'
      appSettings: [
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ORLEANS_AZURE_STORAGE_CONNECTION_STRING'
          value: storageConnectionString
        }
        {
          name: 'ORLEANS_CLUSTER_ID'
          value: 'Default'
        }
        {
          name: 'COSMOS_ENDPOINT'
          value: cosmosEndpoint
        }
        {
          name: 'COSMOS_PRIMARY_KEY'
          value: cosmosPrimaryKey
        }
        {
          name: 'COSMOS_DATABASE_NAME'
          value: cosmosDatabaseName
        }
        {
          name: 'COSMOS_CONTAINER_NAME'
          value: cosmosContainerName
        }
        {
          name: 'EVENTHUB_NAMESPACE_ID'
          value: eventHubNamespaceId
        }
        {
          name: 'EVENTHUB_NAME'
          value: eventHubName
        }
        {
          name: 'CHECKPOINT_STORAGE_CONNECTION_STRING'
          value: storageConnectionString
        }
      ]
      alwaysOn: true
    }
  }
}

resource slotConfig 'Microsoft.Web/sites/config@2021-03-01' = {
  name: 'slotConfigNames'
  parent: appService
  properties: {
    appSettingNames: [
      'ORLEANS_CLUSTER_ID'
    ]
  }
}

resource appServiceConfig 'Microsoft.Web/sites/config@2021-03-01' = {
  parent: appService
  name: 'metadata'
  properties: {
    CURRENT_STACK: 'dotnet'
  }
}

output storageConnectionStringDebug string = storageConnectionString
