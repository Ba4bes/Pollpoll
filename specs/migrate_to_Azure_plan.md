# Azure Deployment Plan for PollPoll

Deploy PollPoll app to Azure App Service with minimal changes, using Azure Files for SQLite persistence and Key Vault to securely store the host authentication token. Fresh database start on Azure (no migration from local).

## Steps

### 1. Remove poll expiration feature
- Delete `PollPoll/BackgroundServices/PollExpirationService.cs` file
- Remove `AddHostedService<PollExpirationService>()` from `PollPoll/Program.cs` (line 37)

### 2. Update SQLite connection string for Azure Files
- In `PollPoll/appsettings.json`, change:
  - From: `"Data Source=polls.db"`
  - To: `"Data Source=/home/data/polls.db;Cache=Shared;Mode=ReadWriteCreate"`
- This improves performance on Azure Files

### 3. Configure Key Vault reference for HostAuth token
- In `PollPoll/appsettings.json`, replace:
  - From: `"HostAuth:Token": "dev-host-token"`
  - To: `"HostAuth:Token": "@Microsoft.KeyVault(SecretUri=https://YOUR-VAULT-NAME.vault.azure.net/secrets/HostAuthToken/)"`
- Enable System-assigned Managed Identity on App Service in Azure portal

### 4. Configure App Service settings
- Enable WebSockets for SignalR
- Set application setting `WEBSITE_ARR_AFFINITY=true` for sticky sessions
- Mount Azure Files storage to `/home/data` path in Configuration → Path mappings

### 5. Set up GitHub deployment sync
- In Deployment Center, select GitHub source
- Authenticate and choose Ba4bes/Pollpoll repository
- Select 001-pulsepoll-app branch
- Save to auto-create GitHub Actions workflow

## Azure Resources Needed

Create these in Azure portal:

1. **Azure Storage Account**
   - Tier: Standard LRS
   - Create file share named "polldata"
   - Cost: ~$0.05/month

2. **Azure App Service**
   - Tier: Basic B1
   - OS: Windows
   - Runtime: .NET 10.0
   - Enable System-assigned Managed Identity
   - Cost: ~$13/month

3. **Azure Key Vault**
   - Tier: Standard
   - Create secret "HostAuthToken" with your secure token
   - Grant App Service Managed Identity "Key Vault Secrets User" role
   - Cost: ~$0.03/month

**Total estimated cost: ~$13/month**

## Post-Deployment Database Setup

After first deployment, create fresh database via SSH:

1. Azure portal → App Service → SSH
2. Run: `cd /home/data && dotnet /home/site/wwwroot/PollPoll.dll` (app will create `polls.db` automatically on first run)
3. Or manually run: `dotnet ef database update --project /home/site/wwwroot/PollPoll.dll`
4. Verify: `ls -la polls.db`

## Architecture Notes

**Kept Simple (No Additional Services):**
- ✅ Built-in SignalR (no Azure SignalR Service needed)
- ✅ IMemoryCache (no Redis needed)
- ✅ Single App Service instance
- ✅ SQLite on Azure Files (no SQL Database needed)
- ✅ Token-based host auth (no Entra ID needed)

**App Configuration:**
- WebSockets: Enabled (for SignalR)
- ARR Affinity: Enabled (sticky sessions)
- Managed Identity: Enabled (for Key Vault access)
- Azure Files Mount: `/home/data` (SQLite database storage)

## Key Vault Configuration

Replace `YOUR-VAULT-NAME` in appsettings.json with your actual Key Vault name after creation.

**Secret to create in Key Vault:**
- Name: `HostAuthToken`
- Value: Your secure token (generate your own)