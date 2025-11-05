param location string
param baseName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: '${baseName}storage'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/imports'
  properties: {}
}

var key = storageAccount.listKeys().keys[0].value
var protocol = 'DefaultEndpointsProtocol=https'
var accountBits = 'AccountName=${storageAccount.name};AccountKey=${key}'
var endpointSuffix = 'EndpointSuffix=${environment().suffixes.storage}'

output connectionString string = '${protocol};${accountBits};${endpointSuffix}'


output storageAccountId string = storageAccount.id
output containerName string = blobContainer.name
