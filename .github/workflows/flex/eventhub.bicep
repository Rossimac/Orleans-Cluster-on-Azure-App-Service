param location string
param baseName string

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2022-10-01-preview' = {
  name: '${baseName}-ehns'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    isAutoInflateEnabled: true
    maximumThroughputUnits: 20
  }
}

resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2022-10-01-preview' = {
  parent: eventHubNamespace
  name: '${baseName}-stream'
  properties: {
    messageRetentionInDays: 1
    partitionCount: 32
  }
}

output eventHubNamespaceId string = eventHubNamespace.id
output eventHubName string = eventHub.name
