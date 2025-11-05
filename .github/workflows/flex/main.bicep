param appName string
param location string = resourceGroup().location

module storageModule 'storage.bicep' = {
  name: 'orleansStorageModule'
  params: {
    baseName: '${appName}storage'
    location: location
  }
}

module cosmos 'cosmos.bicep' = {
  name: 'cosmosDeploy'
  params: {
    databaseName: 'OrleansDb'
    containerName: 'OrleansContainer'
  }
}

module eventhub 'eventhub.bicep' = {
  name: 'eventhubDeploy'
  params: {
    location: location
    baseName: '${appName}eventhub'
  }
}

module logsModule 'logs-and-insights.bicep' = {
  name: 'orleansLogModule'
  params: {
    operationalInsightsName: '${appName}-logs'
    appInsightsName: '${appName}-insights'
    location: location
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2021-05-01' = {
  name: '${appName}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.17.0.0/16'
        '192.168.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '172.17.0.0/24'
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'staging'
        properties: {
          addressPrefix: '192.168.0.0/24'
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

module siloModule 'app-service.bicep' = {
  name: 'orleansSiloModule'
  params: {
    appName: appName
    location: location
    vnetSubnetId: vnet.properties.subnets[0].id
    appInsightsConnectionString: logsModule.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: logsModule.outputs.appInsightsInstrumentationKey
    storageConnectionString: storageModule.outputs.connectionString
    cosmosEndpoint: cosmos.outputs.cosmosEndpoint
    cosmosPrimaryKey: cosmos.outputs.cosmosPrimaryKey
    cosmosDatabaseName: cosmos.outputs.cosmosDatabaseName
    cosmosContainerName: cosmos.outputs.cosmosContainerName
    eventHubNamespaceId: eventhub.outputs.eventHubNamespaceId
    eventHubName: eventhub.outputs.eventHubName
  }
}
