using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using RepairPlannerAgent.Models;

namespace RepairPlannerAgent.Services;

/// <summary>
/// Generates repair plans using Azure AI Foundry (via Azure AI Inference SDK).
/// </summary>
public sealed class AIFoundryService
{
    private readonly ChatCompletionsClient _client;
    private readonly string _modelDeployment;
    private readonly ILogger<AIFoundryService> _logger;

    public AIFoundryService(
        string endpoint,
        string key,
        string modelDeployment,
        ILogger<AIFoundryService>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("AI Foundry endpoint is required.", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("AI Foundry key is required.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(modelDeployment))
        {
            throw new ArgumentException("Model deployment name is required.", nameof(modelDeployment));
        }

        _client = new ChatCompletionsClient(new Uri(endpoint), new AzureKeyCredential(key));
        _modelDeployment = modelDeployment;
        _logger = logger ?? NullLogger<AIFoundryService>.Instance;
    }

    /// <summary>
    /// Generates a repair plan as a <see cref="WorkOrder"/>.
    /// The model is instructed to return a single JSON object matching the WorkOrder schema.
    /// </summary>
    public async Task<WorkOrder> GenerateRepairPlanAsync(
        DiagnosedFault fault,
        IReadOnlyList<Technician> availableTechnicians,
        IReadOnlyList<Part> availableParts,
        CancellationToken cancellationToken = default)
    {
        if (fault is null)
        {
            throw new ArgumentNullException(nameof(fault));
        }

        availableTechnicians ??= Array.Empty<Technician>();
        availableParts ??= Array.Empty<Part>();

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(fault, availableTechnicians, availableParts);

        try
        {
            var options = new ChatCompletionsOptions
            {
                Temperature = 0.2f,
                MaxTokens = 1200,
            };

            options.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
            options.Messages.Add(new ChatRequestUserMessage(userPrompt));


            // Azure.AI.Inference uses the deployment name as the model identifier.
            Response<ChatCompletions> response = await _client.CompleteAsync(
                options,
                cancellationToken);

            var content = response.Value.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("AI response contained no message content.");
            }

            var json = ExtractJsonObject(content);
            var workOrder = JsonConvert.DeserializeObject<WorkOrder>(json);

            if (workOrder is null)
            {
                throw new JsonException("AI response JSON deserialized to null WorkOrder.");
            }

            HydrateDefaults(workOrder, fault);
            return workOrder;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI Foundry JSON response into a WorkOrder.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Foundry call failed in {Method}.", nameof(GenerateRepairPlanAsync));
            throw;
        }
    }

    private static string BuildSystemPrompt()
    {
        return """
You are an expert maintenance planner for industrial tire manufacturing equipment.

Return ONLY valid JSON (no markdown, no backticks, no prose).

The JSON MUST match this C#-compatible schema (camelCase properties):
{
  "id": "string (optional)",
  "workOrderNumber": "string (optional)",
  "machineId": "string",
  "faultType": "string",
  "title": "string",
  "description": "string",
  "type": "preventive|corrective|emergency",
  "priority": "critical|high|medium|low",
  "status": "new|scheduled|in_progress|completed",
  "assignedTo": "string (technician id)",
  "tasks": [
    {
      "description": "string",
      "estimatedMinutes": 0,
      "requiredTools": ["string"]
    }
  ],
  "requiredParts": [
    {
      "partNumber": "string",
      "quantity": 0
    }
  ],
  "estimatedDuration": 0,
  "notes": "string (optional)"
}

Rules:
- Prefer technicians whose skills best match the fault.
- Use only partNumbers present in the provided parts list.
- If a required part is missing, still include it in requiredParts with quantity, and mention it in notes.
- Ensure estimatedDuration equals the sum of task estimatedMinutes.
""";
    }

    private static string BuildUserPrompt(
        DiagnosedFault fault,
        IReadOnlyList<Technician> availableTechnicians,
        IReadOnlyList<Part> availableParts)
    {
        var technicianSummary = availableTechnicians
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                available = t.Available,
                skills = t.Skills,
                shiftSchedule = t.ShiftSchedule,
            })
            .ToArray();

        var partsSummary = availableParts
            .Select(p => new
            {
                partNumber = p.PartNumber,
                name = p.Name,
                quantityInStock = p.QuantityInStock,
                location = p.Location,
            })
            .ToArray();

        var faultSummary = new
        {
            machineId = fault.MachineId,
            faultType = fault.FaultType,
            rootCause = fault.RootCause,
            severity = fault.Severity,
            detectedAt = fault.DetectedAt,
            metadata = fault.Metadata,
        };

        return $"""
Create a repair plan work order for this diagnosed fault:

Fault:
{JsonConvert.SerializeObject(faultSummary, Formatting.Indented)}

Available technicians:
{JsonConvert.SerializeObject(technicianSummary, Formatting.Indented)}

Available parts:
{JsonConvert.SerializeObject(partsSummary, Formatting.Indented)}

Additional requirements:
- Map severity to priority: critical->critical, high->high, medium->medium, low->low.
- If no technicians are available, set assignedTo to an empty string and explain in notes.
- Keep tasks practical and specific for a maintenance technician.
""";
    }

    private static string ExtractJsonObject(string text)
    {
        // Handles responses that accidentally include preamble/prose.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end < 0 || end <= start)
        {
            throw new JsonException("Could not locate a JSON object in the AI response.");
        }

        return text.Substring(start, end - start + 1);
    }

    private static void HydrateDefaults(WorkOrder workOrder, DiagnosedFault fault)
    {
        workOrder.MachineId = string.IsNullOrWhiteSpace(workOrder.MachineId) ? fault.MachineId : workOrder.MachineId;
        workOrder.FaultType = string.IsNullOrWhiteSpace(workOrder.FaultType) ? fault.FaultType : workOrder.FaultType;

        if (workOrder.Tasks is null)
        {
            workOrder.Tasks = new List<RepairTask>();
        }

        if (workOrder.RequiredParts is null)
        {
            workOrder.RequiredParts = new List<PartRequirement>();
        }

        if (string.IsNullOrWhiteSpace(workOrder.Status))
        {
            workOrder.Status = "new";
        }

        // Keep sample dataset alignment: ensure AssignedTo is set when AssignedTechnician is populated.
        if (string.IsNullOrWhiteSpace(workOrder.AssignedTo) && workOrder.AssignedTechnician is not null)
        {
            workOrder.AssignedTo = workOrder.AssignedTechnician.Id;
        }

        // If estimatedDuration omitted, compute from tasks.
        if (workOrder.EstimatedDurationMinutes is null)
        {
            workOrder.EstimatedDurationMinutes = workOrder.Tasks.Sum(t => Math.Max(0, t.EstimatedMinutes));
        }
    }
}
