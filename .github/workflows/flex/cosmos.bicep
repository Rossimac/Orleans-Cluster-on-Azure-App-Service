@description('Cosmos DB account name')
param accountName string = 'cosmos-${uniqueString(resourceGroup().id)}'

@description('Location for the Cosmos DB account.')
param location string = resourceGroup().location

@description('The name for the SQL API database')
param databaseName string

@description('The name for the SQL API container')
param containerName string

param throughputPolicy string = 'autoscale' // 'autoscale' or 'manual'
param autoscaleMaxThroughput int = 4000
param manualThroughput int = 400

resource account 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: toLower(accountName)
  location: location
  properties: {
    enableFreeTier: true
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless' // Remove this if using provisioned throughput
      }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: account
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
    options: {
      throughput: 1000
    }
  }
}


// Container for Shopping Cart Grains
resource shoppingCartContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'shopping-carts'
  properties: {
    resource: {
      id: 'shopping-carts'
      partitionKey: {
        paths: [
          '/PartitionKey'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: -1 // -1 means no default TTL
    }
    options: throughputPolicy == 'autoscale' ? {
      autoscaleSettings: {
        maxThroughput: autoscaleMaxThroughput
      }
    } : {
      throughput: manualThroughput
    }
  }
}

// Container for Product Grains
resource productContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'products'
  properties: {
    resource: {
      id: 'products'
      partitionKey: {
        paths: [
          '/PartitionKey'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: -1
    }
    options: throughputPolicy == 'autoscale' ? {
      autoscaleSettings: {
        maxThroughput: autoscaleMaxThroughput
      }
    } : {
      throughput: manualThroughput
    }
  }
}

// Container for Inventory Grains
resource inventoryContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'inventory'
  properties: {
    resource: {
      id: 'inventory'
      partitionKey: {
        paths: [
          '/PartitionKey'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: -1
    }
    options: throughputPolicy == 'autoscale' ? {
      autoscaleSettings: {
        maxThroughput: autoscaleMaxThroughput
      }
    } : {
      throughput: manualThroughput
    }
  }
}

// Container for Pet Claims Grains
resource petClaimsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'pet-claims'
  properties: {
    resource: {
      id: 'pet-claims'
      partitionKey: {
        paths: [
          '/PartitionKey'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: -1
    }
    options: throughputPolicy == 'autoscale' ? {
      autoscaleSettings: {
        maxThroughput: autoscaleMaxThroughput
      }
    } : {
      throughput: manualThroughput
    }
  }
}

output location string = location
output resourceGroupName string = resourceGroup().name

output cosmosEndpoint string = account.properties.documentEndpoint
output cosmosPrimaryKey string = account.listKeys().primaryMasterKey
output cosmosDatabaseName string = databaseName
output cosmosContainerName string = containerName