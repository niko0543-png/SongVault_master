// Infrastructure Azure de SongVault (ADR 0012). Déployée par scripts/deploy-azure.ps1, jamais à la main.
targetScope = 'resourceGroup'

@description('Région : France Central, données hébergées en UE.')
param location string = 'francecentral'

@description('Préfixe des noms, 3 à 11 minuscules ou chiffres : le nom du compte de stockage en dérive.')
@minLength(3)
@maxLength(11)
param prefix string = 'songvault'

@description('Tag des images publiées par le workflow « images » (ex. v1.1.0).')
param imageTag string

@description('Registre et propriétaire des images.')
param imageRegistry string = 'ghcr.io/niko0543-png'

@description('Mot de passe de l\'administrateur SQL : à conserver, il est redemandé à chaque déploiement.')
@secure()
param sqlAdminPassword string

@allowed(['Log', 'Brevo'])
param emailProvider string = 'Log'

param emailFrom string = 'no-reply@songvault.local'

@secure()
param brevoApiKey string = ''

@description('Paiement en ligne (#16) : None (formules et quotas sans paiement) ou Stripe.')
@allowed(['None', 'Stripe'])
param billingProvider string = 'None'

@secure()
param stripeSecretKey string = ''

@secure()
param stripeWebhookSecret string = ''

@description('Identifiants des prix Stripe (price_…) des formules payantes ; ce ne sont pas des secrets.')
param stripePriceBand string = ''
param stripePriceStudio string = ''

@description('Antivirus des fichiers (#17) : None (fichiers non analysés, sans conteneur ClamAV) ou ClamAv (conteneur à côté de l\'API, environ 15 € par mois).')
@allowed(['None', 'ClamAv'])
param antivirusProvider string = 'None'

@description('Verrous « suppression impossible » sur la base et le stockage. false seulement pour l\'exercice de restauration, supprimé ensuite.')
param enableDeleteLocks bool = true

// #17 : antivirus à côté de l'API, joignable sur localhost:3310 seulement (ADR 0024). Ajouté avec antivirusProvider = ClamAv
var clamavContainer = {
  name: 'clamav'
  image: '${imageRegistry}/songvault-clamav:${imageTag}'
  resources: {
    cpu: json('0.75')
    memory: '1.5Gi'                            // signatures chargées : environ 1,2 Go
  }
  probes: [
    {
      type: 'Startup'                          // chargement des signatures : 5 minutes au plus
      tcpSocket: { port: 3310 }
      periodSeconds: 30
      failureThreshold: 10
    }
    {
      type: 'Liveness'
      tcpSocket: { port: 3310 }
      periodSeconds: 60
    }
  ]
}

var suffix = uniqueString(resourceGroup().id)          // 13 caractères, stable pour ce groupe de ressources
var storageName = take('${prefix}${suffix}', 24)
var keyVaultName = take('${prefix}-kv-${suffix}', 24)
var sqlServerName = '${prefix}-sql-${suffix}'
var apiName = '${prefix}-api'
var webName = '${prefix}-web'
var containerName = 'songvault-files'
var sqlAdminLogin = 'songvaultadmin'

// ---------- Journaux et environnement Container Apps ----------
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// Adresse publique du site, connue avant la création de l'application web
var publicUrl = 'https://${webName}.${env.properties.defaultDomain}'

// ---------- Stockage des fichiers ----------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false                       // aucun conteneur public, jamais
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    // Fichier ou conteneur supprimé : récupérable 14 jours (ADR 0013). Les fichiers ne sont jamais réécrits,
    // la suppression réversible suffit : pas de versions de blobs.
    deleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    // Le navigateur envoie (PUT) et lit (GET) directement, depuis le site seulement
    cors: {
      corsRules: [
        {
          allowedOrigins: [publicUrl]
          allowedMethods: ['GET', 'HEAD', 'PUT']
          allowedHeaders: ['*']
          exposedHeaders: ['*']
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: containerName
  properties: { publicAccess: 'None' }
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var storageOrigin = 'https://${storage.name}.blob.${environment().suffixes.storage}'

// ---------- Base de données ----------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// 0.0.0.0 : services Azure seulement (Container Apps), aucune adresse extérieure
resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Basic (5 DTU, 2 Go) : toujours allumée, prix fixe. Une base « sans serveur » ne se mettrait jamais
// en pause, car le service des tâches de fond l'interroge toutes les 5 secondes.
resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'SongVault'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    requestedBackupStorageRedundancy: 'Local'
  }
}

// Restauration à un instant donné sur 7 jours, le maximum du niveau Basic (ADR 0013)
resource databaseBackups 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01-preview' = {
  parent: database
  name: 'default'
  properties: {
    retentionDays: 7
  }
}

// ---------- Verrous : une suppression par erreur de la base ou du stockage échoue (ADR 0013) ----------
resource databaseLock 'Microsoft.Authorization/locks@2020-05-01' = if (enableDeleteLocks) {
  name: 'songvault-ne-pas-supprimer'
  scope: database
  properties: {
    level: 'CanNotDelete'
    notes: 'Données de SongVault. Retirer le verrou avant une suppression voulue : docs/runbook-sauvegardes.md.'
  }
}

resource storageLock 'Microsoft.Authorization/locks@2020-05-01' = if (enableDeleteLocks) {
  name: 'songvault-ne-pas-supprimer'
  scope: storage
  properties: {
    level: 'CanNotDelete'
    notes: 'Fichiers de SongVault. Retirer le verrou avant une suppression voulue : docs/runbook-sauvegardes.md.'
  }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${database.name};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30'

// ---------- Key Vault : clé qui chiffre les clés Data Protection ----------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: ['wrapKey', 'unwrapKey']
  }
}

// ---------- API ----------
resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiName
  location: location
  identity: { type: 'SystemAssigned' }                 // pour Key Vault, sans secret
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      ingress: {
        external: false                                // joignable seulement par le front, dans l'environnement
        targetPort: 8080
        transport: 'http'
      }
      secrets: [
        { name: 'sql-connection', value: sqlConnectionString }
        { name: 'storage-connection', value: storageConnectionString }
        { name: 'brevo-api-key', value: empty(brevoApiKey) ? 'aucune' : brevoApiKey }   // un secret vide est refusé
        { name: 'stripe-secret-key', value: empty(stripeSecretKey) ? 'aucune' : stripeSecretKey }
        { name: 'stripe-webhook-secret', value: empty(stripeWebhookSecret) ? 'aucune' : stripeWebhookSecret }
      ]
    }
    template: {
      containers: concat([
        {
          name: 'api'
          image: '${imageRegistry}/songvault-api:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__SongVault', secretRef: 'sql-connection' }
            { name: 'FileStorage__Provider', value: 'AzureBlob' }
            { name: 'FileStorage__AzureBlob__ConnectionString', secretRef: 'storage-connection' }
            { name: 'FileStorage__AzureBlob__ContainerName', value: containerName }
            { name: 'Email__Provider', value: emailProvider }
            { name: 'Email__FromAddress', value: emailFrom }
            { name: 'Email__Brevo__ApiKey', secretRef: 'brevo-api-key' }
            { name: 'Billing__Provider', value: billingProvider }
            { name: 'Billing__Stripe__SecretKey', secretRef: 'stripe-secret-key' }
            { name: 'Billing__Stripe__WebhookSecret', secretRef: 'stripe-webhook-secret' }
            { name: 'Billing__Stripe__Prices__band', value: stripePriceBand }
            { name: 'Billing__Stripe__Prices__studio', value: stripePriceStudio }
            { name: 'App__PublicUrl', value: publicUrl }
            { name: 'DataProtection__KeyVaultKeyUri', value: dataProtectionKey.properties.keyUri }
            { name: 'Antivirus__Provider', value: antivirusProvider }
            { name: 'Antivirus__ClamAv__Host', value: 'localhost' }   // #17 : le conteneur clamav (clamavContainer)
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
            }
          ]
        }
      ], antivirusProvider == 'ClamAv' ? [ clamavContainer ] : [])   // #17 : ClamAV seulement une fois activé
      scale: {
        minReplicas: 1                                 // au moins une : les tâches de fond tournent dans l'API
        maxReplicas: 2
      }
    }
  }
}

// « Key Vault Crypto Service Encryption User » : chiffrer et déchiffrer avec la clé, rien d'autre
var keyVaultCryptoUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'e147488a-f6f5-4113-8e2d-b22465e65bf6')

resource apiKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, api.id, keyVaultCryptoUser)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultCryptoUser
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Front (nginx) ----------
resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: webName
  location: location
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false                           // HTTP redirigé vers HTTPS
      }
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${imageRegistry}/songvault-web:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'API_UPSTREAM', value: 'http://${api.name}' }
            { name: 'STORAGE_ORIGIN', value: storageOrigin }
          ]
        }
      ]
      scale: {
        minReplicas: 0                                 // s'arrête sans visiteur ; quelques secondes au réveil
        maxReplicas: 2
      }
    }
  }
}

// ---------- Migrations : job lancé par le script de déploiement (ADR 0005) ----------
resource migrator 'Microsoft.App/jobs@2024-03-01' = {
  name: '${prefix}-migrator'
  location: location
  properties: {
    environmentId: env.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 600
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      secrets: [
        { name: 'sql-connection', value: sqlConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'migrator'
          image: '${imageRegistry}/songvault-migrator:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ConnectionStrings__SongVault', secretRef: 'sql-connection' }
          ]
        }
      ]
    }
  }
}

output publicUrl string = publicUrl
output apiName string = api.name
output migratorName string = migrator.name
output storageAccountName string = storage.name