param location string
param baseName string

// Namespace
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

// Event Hub
resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2024-01-01' = {
  parent: eventHubNamespace
  name: '${baseName}-stream'
  properties: {
    messageRetentionInDays: 1
    partitionCount: 32
  }
}

// Namespace-level shared access policy (authorization rule)
resource eventHubListenPolicy 'Microsoft.EventHub/namespaces/authorizationRules@2024-01-01' = {
  parent: eventHubNamespace
  name: 'ListenPolicy'
  properties: {
    rights: [
      'Listen'
      'Send'
    ]
  }
}

output eventHubNamespaceId string = eventHubNamespace.id
output eventHubName string = eventHub.name

output eventHubConnectionString string = listKeys(
  resourceId(
    'Microsoft.EventHub/namespaces/authorizationRules',
    eventHubNamespace.name,
    eventHubListenPolicy.name
  ),
  '2024-01-01'
).primaryConnectionString