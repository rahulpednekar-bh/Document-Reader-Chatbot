# Azure Setup Guide

Step-by-step guide to provision all required Azure resources for the Document Reader Chatbot.

## Prerequisites

- An Azure subscription
- Azure CLI installed: `az --version`
- Logged in: `az login`

---

## 1. Create a Resource Group

```bash
az group create \
  --name rg-docreader \
  --location eastus
```

---

## 2. Azure Blob Storage

```bash
az storage account create \
  --name stdocreader$RANDOM \
  --resource-group rg-docreader \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2

# Create the documents container (private)
az storage container create \
  --name documents \
  --account-name <storage-account-name>
```

**Copy:** Storage account name and connection string for `local.settings.json`:
```bash
az storage account show-connection-string \
  --name <storage-account-name> \
  --resource-group rg-docreader \
  --query connectionString -o tsv
```

---

## 3. Azure Cosmos DB

```bash
az cosmosdb create \
  --name cosmos-docreader-$RANDOM \
  --resource-group rg-docreader \
  --kind GlobalDocumentDB \
  --default-consistency-level Session

# Create database
az cosmosdb sql database create \
  --account-name <cosmos-account-name> \
  --resource-group rg-docreader \
  --name docreader

# Create containers
az cosmosdb sql container create \
  --account-name <cosmos-account-name> \
  --resource-group rg-docreader \
  --database-name docreader \
  --name documents \
  --partition-key-path /id

az cosmosdb sql container create \
  --account-name <cosmos-account-name> \
  --resource-group rg-docreader \
  --database-name docreader \
  --name sessions \
  --partition-key-path /id
```

**Copy:** Connection string for `local.settings.json`:
```bash
az cosmosdb keys list \
  --name <cosmos-account-name> \
  --resource-group rg-docreader \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

---

## 4. Azure AI Document Intelligence

This resource is required for automatic OCR of scanned PDFs. If all your documents contain a text layer, this step can be skipped — but any scanned PDF uploads will fail at the OCR step.

```bash
az cognitiveservices account create \
  --name docreader-docintellect \
  --resource-group rg-docreader \
  --kind FormRecognizer \
  --sku S0 \
  --location eastus \
  --yes
```

**Copy:** Endpoint and key for `local.settings.json`:
```bash
# Endpoint
az cognitiveservices account show \
  --name docreader-docintellect \
  --resource-group rg-docreader \
  --query properties.endpoint -o tsv

# Key (used locally; production uses Managed Identity instead)
az cognitiveservices account keys list \
  --name docreader-docintellect \
  --resource-group rg-docreader \
  --query key1 -o tsv
```

> **Production note:** In Azure, the Function App's Managed Identity is granted the `Cognitive Services User` role (see step 5). The key is only needed for local development via `local.settings.json`.

---

## 5. Azure AI Foundry Project

> Azure AI Foundry is provisioned through the Azure Portal or AI Foundry Studio.

1. Go to [Azure AI Foundry Studio](https://ai.azure.com)
2. Create a new **Hub** (or use an existing one) in `rg-docreader`
3. Create a new **Project** inside the Hub (e.g., `docreader-project`)
4. In the Project, go to **Models + endpoints** → **Deploy model**
   - Deploy: `gpt-4o-mini` (for chat completions)
5. Go to **Agents** → **Create agent**
   - Name: `Document Reader Agent`
   - Model: `gpt-4o-mini`
   - Instructions: `You are a document assistant. Answer questions based only on the content of the uploaded documents. If the answer is not found in the documents, say so clearly.`
   - Tools: Enable **File Search**
6. Create a **Vector Store** and note its ID
7. Attach the Vector Store to the Agent

**Copy for `local.settings.json`:**
- Project connection string: **Project** → **Overview** → **Connection string**
- Agent ID: visible in the Agent detail page
- Vector Store ID: visible in the Vector Store detail page

---

## 6. Azure Functions App

```bash
# Storage for Functions runtime (can reuse the one above)
az functionapp create \
  --resource-group rg-docreader \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --name func-docreader-api \
  --storage-account <storage-account-name>
```

### Assign Managed Identity

```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name func-docreader-api \
  --resource-group rg-docreader

# Copy the principalId from output, then grant roles:
PRINCIPAL_ID=$(az functionapp identity show \
  --name func-docreader-api \
  --resource-group rg-docreader \
  --query principalId -o tsv)

# Blob Storage: Storage Blob Data Contributor
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/<sub-id>/resourceGroups/rg-docreader/providers/Microsoft.Storage/storageAccounts/<storage-account-name>

# Cosmos DB: Cosmos DB Built-in Data Contributor
az cosmosdb sql role assignment create \
  --account-name <cosmos-account-name> \
  --resource-group rg-docreader \
  --role-definition-name "Cosmos DB Built-in Data Contributor" \
  --principal-id $PRINCIPAL_ID \
  --scope /subscriptions/<sub-id>/resourceGroups/rg-docreader/providers/Microsoft.DocumentDB/databaseAccounts/<cosmos-account-name>

# AI Foundry / Cognitive Services: Cognitive Services User
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Cognitive Services User" \
  --scope /subscriptions/<sub-id>/resourceGroups/rg-docreader

# Azure AI Document Intelligence: Cognitive Services User
# (The scope above covers the whole resource group, which includes Document Intelligence.
#  If you prefer a narrower scope, target the resource directly:)
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Cognitive Services User" \
  --scope /subscriptions/<sub-id>/resourceGroups/rg-docreader/providers/Microsoft.CognitiveServices/accounts/docreader-docintellect
```

### Configure App Settings

```bash
az functionapp config appsettings set \
  --name func-docreader-api \
  --resource-group rg-docreader \
  --settings \
    "AzureFoundry__ConnectionString=<foundry-connection-string>" \
    "AzureFoundry__AgentId=<agent-id>" \
    "AzureFoundry__VectorStoreId=<vector-store-id>" \
    "BlobStorage__AccountName=<storage-account-name>" \
    "BlobStorage__ContainerName=documents" \
    "CosmosDB__AccountEndpoint=https://<cosmos-account-name>.documents.azure.com:443/" \
    "CosmosDB__DatabaseName=docreader" \
    "AzureDocumentIntelligence__Endpoint=https://docreader-docintellect.cognitiveservices.azure.com/"
```

> In Azure (production), `AzureDocumentIntelligence__Key` is **not** set — the Function App uses Managed Identity (`DefaultAzureCredential`) to authenticate with Document Intelligence via the `Cognitive Services User` role assigned above. The key is only used in `local.settings.json` for local development.

---

## 7. Azure Static Web Apps (Frontend)

1. In Azure Portal → Create **Static Web App**
2. Link to your GitHub repository
3. Set **App location** to `/frontend`
4. Set **Output location** to `dist/frontend/browser`
5. After creation, go to **Configuration** → add environment variable:
   - `API_BASE_URL` = `https://func-docreader-api.azurewebsites.net/api`

The first push to your linked branch will trigger a build and deploy.

---

## 8. `local.settings.json` Reference

After completing the steps above, populate `backend/DocumentChatbot.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<storage-connection-string>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureFoundry__ConnectionString": "<ai-foundry-project-connection-string>",
    "AzureFoundry__AgentId": "<agent-id>",
    "AzureFoundry__VectorStoreId": "<vector-store-id>",
    "BlobStorage__ConnectionString": "<storage-connection-string>",
    "BlobStorage__ContainerName": "documents",
    "CosmosDB__ConnectionString": "<cosmos-db-connection-string>",
    "CosmosDB__DatabaseName": "docreader",
    "AzureDocumentIntelligence__Endpoint": "https://<your-resource-name>.cognitiveservices.azure.com/",
    "AzureDocumentIntelligence__Key": "<your-document-intelligence-key>"
  },
  "Host": {
    "LocalHttpPort": 7071,
    "CORS": "http://localhost:4200",
    "CORSCredentials": false
  }
}
```
