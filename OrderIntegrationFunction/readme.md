# Azure Functions Event-Driven Order System

## 📌 Overview

This project demonstrates a **real-world event-driven architecture** using:

* Azure Functions (.NET 8)
* Azure Service Bus (Queue + Topic)
* Azure Blob Storage
* Azure Key Vault
* Application Insights
* GitHub Actions (CI/CD)

It simulates an **Order Processing System** with:

* HTTP API → create order
* Blob storage → payload storage
* Service Bus → messaging
* Multiple consumers (Billing, Notification)
* Retry + DLQ handling
* Idempotency
* Environment-based deployment

---

## 🏗️ Architecture

```
Client (curl / Angular)
        |
        v
CreateOrderEcho (HTTP Function)
        |
        v
Blob Storage (orders-incoming)
        |
        v
Service Bus Topic (order-events)
        |
        +------------------------+
        |                        |
        v                        v
Billing Function         Notification Function
        |
        v
Processed / Failed Blobs
```

---

## ⚙️ Azure Resources

### Required

* Azure Function App
* Storage Account (Blob)
* Service Bus Namespace
* Key Vault
* Application Insights

---

## 🔐 Key Vault Secrets

```
BlobStorageConnection
ServiceBusConnection           (Basic - Queue)
ServiceBusTopicConnection      (Standard - Topic)
```

---

## ⚙️ Function App Settings

### Blob

```
BlobSettings__IncomingContainerName = orders-incoming
BlobSettings__ProcessedContainerName = orders-processed
BlobSettings__FailedContainerName = orders-failed
BlobSettings__IdempotencyContainerName = orders-idempotency
```

---

### Service Bus (Queue)

```
ServiceBusSettings__QueueName = orders-echo
ServiceBusConnection = @Microsoft.KeyVault(...)
```

---

### Service Bus (Topic)

```
ServiceBusTopicSettings__TopicName = order-events
ServiceBusTopicConnection = @Microsoft.KeyVault(...)
```

---

## 📬 Service Bus Setup

### Queue (Basic tier)

```
orders-echo
```

Used for:

* simple processing
* retry + DLQ demo

---

### Topic (Standard tier)

```
Topic: order-events
```

Subscriptions:

```
billing-subscription
notification-subscription
```

---

## 🔎 Subscription Filters

### Billing

```
EventType = 'BillingRequired'
```

### Notification

```
EventType = 'NotificationRequired'
```

---

## 🔁 Retry & DLQ

* Service Bus retries automatically
* MaxDeliveryCount controls retry attempts
* Failed messages go to:

```
orders-echo/$DeadLetterQueue
```

---

## 🧠 Idempotency

Implemented using Blob Storage:

```
orders-idempotency/<correlationId>.json
```

Flow:

1. Check if marker exists
2. If exists → skip processing
3. If not → process and create marker

---

## 🚀 GitHub Actions (CI/CD)

### Workflow file

```
.github/workflows/deploy-function.yml
```

### YAML

```yaml
name: Build and deploy Azure Function App

on:
  push:
    branches:
      - develop
  workflow_dispatch:
    inputs:
      target_environment:
        description: 'Environment to deploy to'
        required: true
        default: 'dev'
        type: choice
        options:
          - dev
          - qa
          - prod

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    environment: ${{ inputs.target_environment }}

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - run: dotnet build ./OrderIntegrationFunction --configuration Release

      - run: dotnet publish ./OrderIntegrationFunction --configuration Release --output ./publish

      - uses: Azure/functions-action@v1
        with:
          app-name: ${{ vars.AZURE_FUNCTIONAPP_NAME }}
          package: ./publish
          publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

---

## 🌿 Branch Strategy

```
feature/*   -> manual deploy to dev
develop     -> auto deploy to dev
release/*   -> deploy to qa
main        -> deploy to prod (approval)
```

---

## 🧪 Testing

### Create Order (Queue)

```bash
curl -X POST "https://<function-app>/api/orders/echo?code=<key>" \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"test@test.com","amount":100,"orderStatus":1}'
```

---

### Create Order (Topic)

```bash
curl -X POST "https://<function-app>/api/orders/topic-echo?code=<key>" \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"topic@test.com","amount":100,"orderStatus":1}'
```

---

## 📊 Kusto Queries

### All traces

```kusto
traces
| order by timestamp desc
```

---

### By correlationId

```kusto
traces
| where message contains "<correlationId>"
| order by timestamp asc
```

---

### Function execution

```kusto
requests
| order by timestamp desc
```

---

### Exceptions

```kusto
exceptions
| order by timestamp desc
```

---

### Retries

```kusto
traces
| where message contains "DeliveryCount"
```

---

## 🧹 Cleanup (Reduce Cost)

### Delete Standard Service Bus

1. Go to Azure Portal
2. Delete namespace:

```
<your-standard-namespace>
```

---

### Remove settings

```
ServiceBusTopicConnection
ServiceBusTopicSettings__TopicName
```

---

### Remove Key Vault secret

```
ServiceBusTopicConnection
```

---

## 🔄 Recreate Steps

1. Create Storage Account
2. Create Function App
3. Create Key Vault
4. Add secrets
5. Create Service Bus (Basic or Standard)
6. Add app settings
7. Deploy via GitHub Actions
8. Test with curl

---

## 🎯 What This Project Demonstrates

* Event-driven architecture
* Publish/Subscribe pattern
* Retry & DLQ handling
* Idempotency
* Observability
* CI/CD pipelines
* Environment-based deployment

---

## 🚀 Future Improvements

* Redis for idempotency
* API Management (gateway)
* Distributed tracing (Activity)
* Durable Functions
* Container Apps
* Angular frontend

```
```
