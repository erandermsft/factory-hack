using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RepairPlannerAgent.Models;
using RepairPlannerAgent.Services;

namespace RepairPlannerAgent;

/// <summary>
/// Orchestrates the Repair Planner workflow:
/// 1) Determine required skills/parts from a diagnosed fault
/// 2) Query Cosmos DB for technicians and parts
/// 3) Call AI Foundry to generate a detailed repair plan
/// 4) Persist the resulting work order back to Cosmos DB
/// </summary>
public sealed class RepairPlanner
{
    private readonly CosmosDbService _cosmosService;
    private readonly AIFoundryService _aiService;
    private readonly ILogger<RepairPlanner> _logger;

    public RepairPlanner(
        CosmosDbService cosmosService,
        AIFoundryService aiService,
        ILogger<RepairPlanner> logger)
    {
        _cosmosService = cosmosService ?? throw new ArgumentNullException(nameof(cosmosService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkOrder> PlanRepairAsync(DiagnosedFault fault, CancellationToken cancellationToken = default)
    {
        if (fault is null)
        {
            throw new ArgumentNullException(nameof(fault));
        }

        if (string.IsNullOrWhiteSpace(fault.MachineId))
        {
            throw new ArgumentException("Fault MachineId is required.", nameof(fault));
        }

        if (string.IsNullOrWhiteSpace(fault.FaultType))
        {
            throw new ArgumentException("Fault FaultType is required.", nameof(fault));
        }

        _logger.LogInformation(
            "Planning repair for machine {MachineId} faultType={FaultType} severity={Severity}",
            fault.MachineId,
            fault.FaultType,
            fault.Severity);

        var requiredSkills = DetermineRequiredSkills(fault.FaultType);
        var requiredPartNumbers = DetermineRequiredParts(fault.FaultType);

        _logger.LogInformation("Required skills: {Skills}", string.Join(", ", requiredSkills));
        _logger.LogInformation("Required parts: {Parts}", string.Join(", ", requiredPartNumbers));

        // Technicians
        var technicians = await _cosmosService.GetAvailableTechniciansWithSkillsAsync(
            requiredSkills,
            department: "Maintenance",
            requireAllSkills: false,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Found {TechnicianCount} available technician(s) matching required skills.",
            technicians.Count);

        var orderedTechnicians = RankTechniciansBySkillMatch(technicians, requiredSkills);

        if (orderedTechnicians.Count > 0)
        {
            var topCandidates = orderedTechnicians
                .Take(3)
                .Select(t => t.Id)
                .ToArray();

            _logger.LogInformation(
                "Top technician candidates (by skill match): {TechnicianIds}",
                string.Join(", ", topCandidates));
        }

        // Parts
        var parts = await _cosmosService.GetPartsByPartNumbersAsync(
            requiredPartNumbers,
            category: null,
            cancellationToken: cancellationToken);

        // Ensure the AI sees required parts even if they are missing from inventory.
        var partsForAi = EnsureAllRequiredPartsPresent(parts, requiredPartNumbers);

        if (requiredPartNumbers.Count > 0)
        {
            var missing = partsForAi
                .Where(p => p.QuantityInStock <= 0)
                .Select(p => p.PartNumber)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missing.Length > 0)
            {
                _logger.LogWarning("Missing/zero-stock parts: {PartNumbers}", string.Join(", ", missing));
            }
        }

        // AI plan
        var workOrder = await _aiService.GenerateRepairPlanAsync(
            fault,
            orderedTechnicians,
            partsForAi,
            cancellationToken);

        // Normalize key fields.
        workOrder.MachineId = fault.MachineId;
        workOrder.FaultType ??= fault.FaultType;

        // If the model didn't pick an assignee, pick the best match (if any).
        var selectedTechnician = orderedTechnicians.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(workOrder.AssignedTo) && selectedTechnician is not null)
        {
            workOrder.AssignedTo = selectedTechnician.Id;
            workOrder.AssignedTechnician = selectedTechnician;
        }

        // Ensure a sane status for Cosmos partitioning.
        workOrder.Status = string.IsNullOrWhiteSpace(workOrder.Status) ? "scheduled" : workOrder.Status;

        // Persist
        var id = await _cosmosService.CreateWorkOrderAsync(workOrder, cancellationToken);
        workOrder.Id = id;

        _logger.LogInformation(
            "Work order created id={Id} workOrderNumber={WorkOrderNumber} assignedTo={AssignedTo}",
            workOrder.Id,
            workOrder.WorkOrderNumber,
            workOrder.AssignedTo);

        return workOrder;
    }

    private static IReadOnlyList<Technician> RankTechniciansBySkillMatch(
        IReadOnlyList<Technician> technicians,
        IReadOnlyList<string> requiredSkills)
    {
        if (technicians.Count == 0)
        {
            return Array.Empty<Technician>();
        }

        if (requiredSkills.Count == 0)
        {
            return technicians;
        }

        var required = new HashSet<string>(requiredSkills, StringComparer.OrdinalIgnoreCase);

        return technicians
            .OrderByDescending(t => t.Skills?.Count(s => required.Contains(s)) ?? 0)
            .ThenBy(t => t.Name)
            .ToArray();
    }

    private IReadOnlyList<string> DetermineRequiredSkills(string faultType)
    {
        if (FaultToSkills.TryGetValue(faultType, out var skills) && skills.Count > 0)
        {
            return skills;
        }

        _logger.LogWarning("Unknown faultType '{FaultType}' - using safe default skills.", faultType);
        return new[] { "General Maintenance" };
    }

    private IReadOnlyList<string> DetermineRequiredParts(string faultType)
    {
        if (FaultToParts.TryGetValue(faultType, out var parts))
        {
            return parts;
        }

        // No parts mapped is OK.
        return Array.Empty<string>();
    }

    private static IReadOnlyList<Part> EnsureAllRequiredPartsPresent(
        IReadOnlyList<Part> partsFromInventory,
        IReadOnlyList<string> requiredPartNumbers)
    {
        if (requiredPartNumbers.Count == 0)
        {
            return partsFromInventory;
        }

        var inventoryByPartNumber = partsFromInventory
            .Where(p => !string.IsNullOrWhiteSpace(p.PartNumber))
            .GroupBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var merged = new List<Part>(capacity: Math.Max(partsFromInventory.Count, requiredPartNumbers.Count));

        foreach (var partNumber in requiredPartNumbers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (inventoryByPartNumber.TryGetValue(partNumber, out var found))
            {
                merged.Add(found);
            }
            else
            {
                merged.Add(new Part
                {
                    Id = $"missing-{partNumber}",
                    PartNumber = partNumber,
                    Name = "(not found in inventory)",
                    QuantityInStock = 0,
                });
            }
        }

        // Also include any extra parts that were fetched (shouldn't happen, but safe).
        foreach (var extra in partsFromInventory)
        {
            if (!merged.Any(p => string.Equals(p.PartNumber, extra.PartNumber, StringComparison.OrdinalIgnoreCase)))
            {
                merged.Add(extra);
            }
        }

        return merged;
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> FaultToSkills =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["curing_temperature_excessive"] = new[]
            {
                "tire_curing_press",
                "temperature_control",
                "instrumentation",
                "electrical_systems",
                "plc_troubleshooting",
                "mold_maintenance"
            },
            ["curing_cycle_time_deviation"] = new[]
            {
                "tire_curing_press",
                "plc_troubleshooting",
                "mold_maintenance",
                "bladder_replacement",
                "hydraulic_systems",
                "instrumentation"
            },
            ["building_drum_vibration"] = new[]
            {
                "tire_building_machine",
                "vibration_analysis",
                "bearing_replacement",
                "alignment",
                "precision_alignment",
                "drum_balancing",
                "mechanical_systems"
            },
            ["ply_tension_excessive"] = new[]
            {
                "tire_building_machine",
                "tension_control",
                "servo_systems",
                "precision_alignment",
                "sensor_alignment",
                "plc_programming"
            },
            ["extruder_barrel_overheating"] = new[]
            {
                "tire_extruder",
                "temperature_control",
                "rubber_processing",
                "screw_maintenance",
                "instrumentation",
                "electrical_systems",
                "motor_drives"
            },
            ["low_material_throughput"] = new[]
            {
                "tire_extruder",
                "rubber_processing",
                "screw_maintenance",
                "motor_drives",
                "temperature_control"
            },
            ["high_radial_force_variation"] = new[]
            {
                "tire_uniformity_machine",
                "data_analysis",
                "measurement_systems",
                "tire_building_machine",
                "tire_curing_press"
            },
            ["load_cell_drift"] = new[]
            {
                "tire_uniformity_machine",
                "load_cell_calibration",
                "measurement_systems",
                "sensor_alignment",
                "instrumentation"
            },
            ["mixing_temperature_excessive"] = new[]
            {
                "banbury_mixer",
                "temperature_control",
                "rubber_processing",
                "instrumentation",
                "electrical_systems",
                "mechanical_systems"
            },
            ["excessive_mixer_vibration"] = new[]
            {
                "banbury_mixer",
                "vibration_analysis",
                "bearing_replacement",
                "alignment",
                "mechanical_systems",
                "preventive_maintenance"
            },
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> FaultToParts =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["curing_temperature_excessive"] = new[] { "TCP-HTR-4KW", "GEN-TS-K400" },
            ["curing_cycle_time_deviation"] = new[] { "TCP-BLD-800", "TCP-SEAL-200" },
            ["building_drum_vibration"] = new[] { "TBM-BRG-6220" },
            ["ply_tension_excessive"] = new[] { "TBM-LS-500N", "TBM-SRV-5KW" },
            ["extruder_barrel_overheating"] = new[] { "EXT-HTR-BAND", "GEN-TS-K400" },
            ["low_material_throughput"] = new[] { "EXT-SCR-250", "EXT-DIE-TR" },
            ["high_radial_force_variation"] = Array.Empty<string>(),
            ["load_cell_drift"] = new[] { "TUM-LC-2KN", "TUM-ENC-5000" },
            ["mixing_temperature_excessive"] = new[] { "BMX-TIP-500", "GEN-TS-K400" },
            ["excessive_mixer_vibration"] = new[] { "BMX-BRG-22320", "BMX-SEAL-DP" },
        };
}
