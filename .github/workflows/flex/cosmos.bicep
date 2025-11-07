@description('Cosmos DB account name')
param accountName string = 'cosmos-${uniqueString(resourceGroup().id)}'

@description('Location for the Cosmos DB account.')
param location string = resourceGroup().location

@description('The name for the SQL API database')
param databaseName string

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

// Container for General Grains
resource generalContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'general'
  properties: {
    resource: {
      id: 'general'
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
