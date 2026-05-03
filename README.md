# SAP → Dynamics 365 Smart Mapping (Azure Functions .NET 8 Isolated)

AI-first SAP-to-Dynamics account mapping engine using Azure Functions, Service Bus trigger, and Azure OpenAI.

## Architecture

1. Logic App validates SAP schema and writes to Service Bus queue.
2. Azure Function (`SapAccountMappingFunction`) triggers from queue.
3. Function calls Azure OpenAI to semantically map SAP payload to strict Dynamics 365 Account JSON.
4. Function validates and normalizes AI output.
5. Function logs final Dynamics payload for downstream Dataverse upsert (TODO).

## Project structure

- `Functions/SapAccountMappingFunction.cs`
- `Application/Services/`
  - `IOpenAiMappingService.cs`
  - `OpenAiMappingService.cs`
  - `IDynamicsPayloadValidator.cs`
  - `DynamicsPayloadValidator.cs`
  - `IDataverseService.cs` (placeholder only)
- `Domain/Models/`
  - `SapServiceBusMessage.cs`
  - `SapOrganizationAccountPayload.cs`
  - `DynamicsAccountPayload.cs`
  - `ValidationResult.cs`
- `Infrastructure/Services/PayloadLoggingService.cs`
- `Program.cs`
- `host.json`
- `local.settings.json.example`
- `samples/`

## Required settings

Set in `local.settings.json` (copy from `local.settings.json.example`):

- `ServiceBusConnection`
- `ServiceBus__SapAccountQueueName`
- `AzureOpenAI__Endpoint`
- `AzureOpenAI__ApiKey`
- `AzureOpenAI__DeploymentName`
- `AzureOpenAI__ApiVersion`

## Run locally

```bash
dotnet restore
dotnet build
func start
```

> Requires Azure Functions Core Tools v4 and .NET 8 SDK.

## Trigger contract

Service Bus trigger:

```csharp
[ServiceBusTrigger("%ServiceBus__SapAccountQueueName%", Connection = "ServiceBusConnection")]
```

HTTP trigger (for simple response testing, no Dataverse call):

```csharp
[HttpTrigger(AuthorizationLevel.Function, "post")]
```

## Validation rules

- AI output must be valid JSON object.
- No fields outside Dynamics schema allowed.
- Required: `name`, `accountnumber`, `vsi_sapbusinesspartnerid`, `vsi_sapcustomerid`.
- `creditlimit` must be numeric or null.
- Non-null mapped values must be grounded in source `organizationAccount` values (anti-hallucination check).
- Empty strings are normalized to null.
- Validation failures throw exception to allow Service Bus retry / DLQ.

## Observability

Structured logs include:

- `correlationId`
- `eventType`
- `mappingStatus`
- `validationErrors` (if any)

## Samples

- SAP Service Bus message: `samples/sample-sap-servicebus-message.json`
- Expected Dynamics output: `samples/sample-expected-dynamics-output.json`
