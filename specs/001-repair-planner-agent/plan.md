# Implementation Plan: Repair Planner Agent

**Branch**: `001-repair-planner-agent` | **Date**: 2026-01-14 | **Spec**: `specs/001-repair-planner-agent/spec.md`
**Input**: Feature specification from `specs/001-repair-planner-agent/spec.md`

## Summary

Build an intelligent Repair Planner Agent (component #3 in the multi-agent system) that converts a diagnosed fault into a structured repair plan and a work order.

The agent will:
- Derive required skills and required parts for the diagnosed fault
- Validate prerequisites (technician skills + availability, parts inventory)
- Use Microsoft Foundry to generate a comprehensive, structured plan
- Create a structured Work Order in the ERP system

## Technical Context

**Language/Version**: .NET 10 / C# (console app + dependency injection)
**Primary Dependencies**: HTTP client, configuration, logging, JSON serialization; optional SDK for Microsoft Foundry
**Storage**: Cosmos DB for fault → repair mapping (skills + parts)
**Testing**: Unit tests for orchestration and parsing; integration tests for external API adapters (HR/Part/ERP) using mocks or test doubles
**Target Platform**: Linux container / server runtime
**Project Type**: Single project
**Performance Goals**:
- Generate a plan in under 2 minutes end-to-end under normal dependency latency
- Handle bursts of diagnosed faults without blocking the system (async I/O)
**Constraints**:
- External dependencies (HR/ERP/Parts/Foundry) may be unavailable or slow
- Must produce actionable failure reasons when blocked
- Must have audit-friendly logs without leaking sensitive content
**Scale/Scope**:
- Starts with a single plant / line, expandable to multiple lines

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Quality**: Clear naming, small methods, consistent formatting; XML docs on public APIs.
- **Modularity**: Separate “core planning” from “external integrations” and “data access” with explicit interfaces.
- **Testing (required)**:
  - New logic includes automated tests.
  - JSON parsing / prompt-output handling includes regression tests.
- **Integration**: All adapters (Cosmos/HR/Parts/ERP/Foundry) have contract expectations documented; integration tests exist at boundaries.
- **Change safety**: Avoid large rewrites; isolate behavior changes from refactors.

## Project Structure

### Documentation (this feature)

```text
specs/001-repair-planner-agent/
├── spec.md              # Feature spec (/speckit.specify)
├── plan.md              # This plan (/speckit.plan)
└── checklists/
    └── requirements.md  # Spec quality checklist (/speckit.specify)
```

### Source Code (repository root)

```text
src/RepairPlannerAgent/
├── Models/
│   ├── DiagnosedFault.cs
│   ├── Technician.cs
│   ├── Part.cs
│   ├── WorkOrder.cs
│   ├── WorkOrderTask.cs
│   └── SkillRequirement.cs
├── Services/
│   ├── CosmosDbService.cs
│   ├── HRService.cs
│   ├── PartService.cs
│   ├── ERPService.cs
│   └── AIFoundryService.cs
├── Planning/
│   ├── RepairPlanner.cs
│   └── RepairPlannerOptions.cs
├── Contracts/
│   ├── HrApiContracts.md
│   ├── PartApiContracts.md
│   ├── ErpApiContracts.md
│   └── FoundryContracts.md
└── Program.cs

tests/RepairPlannerAgent.Tests/
├── RepairPlannerTests.cs
├── AIFoundryServiceParsingTests.cs
└── CosmosDbServiceMappingTests.cs
```

**Structure Decision**: A single console application with clear layering:
- Models: pure data objects
- Services: external systems + Cosmos mapping
- Planning: orchestration and business flow
- Program: composition root (DI, config, logging)

## Core Data Model (required)

### DiagnosedFault (input)
Fields needed (minimum):
- Identifier, machine identifier, timestamp
- Fault code/category, severity, diagnostic confidence
- Symptom detail text and optional structured attributes

### Technician
Fields needed (minimum):
- Identifier, display name
- Skills (set of skill codes)
- Availability status + optional next available timestamp/window

### Part (inventory item)
Fields needed (minimum):
- Part number, description
- Available quantity, reserved quantity (if provided)
- Location / warehouse (if relevant)

### WorkOrder (output)
Fields needed (minimum):
- Identifier (ERP-generated)
- Machine identifier + fault reference
- Priority/severity
- Task list (each with required skill(s), estimated effort, safety notes)
- Assigned technician(s)
- Required parts list (part number + quantity)
- Readiness status: ready / blocked / needs review

## Service Responsibilities

### CosmosDbService
Purpose: map fault types to repair prerequisites.

Required behaviors:
- Query skills required to repair a particular fault
- Query parts required for a specific repair

Key design notes:
- Treat mappings as data-driven (fault type → required skills + required parts)
- Return “unknown fault mapping” as a distinct result so RepairPlanner can fallback safely
- Use async methods and timeouts

### HRService
Purpose: technician eligibility + availability.

Required behaviors:
- Query technicians by required skills
- Use the HR API

Key design notes:
- Prefer returning ranked candidates (best match first)
- Availability is time-sensitive; include “as-of timestamp” in results

### PartService
Purpose: inventory readiness.

Required behaviors:
- Fetch parts inventory by part numbers
- Use the Part API

Key design notes:
- Handle partial results (some parts missing)
- Separate “not found” vs “found but insufficient quantity”

### ERPService
Purpose: create work orders.

Required behaviors:
- Create work orders
- Use the ERP API

Key design notes:
- Create work order only when readiness is ready, unless an explicit override mechanism exists
- Persist enough data for traceability (fault reference, tasks, parts, assignment)

### AIFoundryService
Purpose: generate comprehensive plan and convert it into a WorkOrder.

Required behaviors:
- Accept a diagnosed fault, available technicians, and parts
- Build a structured prompt for the LLM
- Parse the response into a WorkOrder object
- Handle JSON deserialization errors

Key design notes:
- Require the model to return JSON only (no prose)
- Validate JSON structure and required fields before returning WorkOrder
- On deserialization failures:
  - log a redacted snippet of the response
  - return a safe failure result with actionable guidance

## Orchestration: RepairPlanner

The `RepairPlanner` class orchestrates:

1. Determining required skills from fault type (Cosmos mapping)
2. Querying available technicians based on skills needed (HR API)
3. Determining what parts are needed based on fault type (Cosmos mapping)
4. Checking parts inventory (Part API)
5. Generating the repair plan with AI (Foundry)
6. Creating the work order in ERP (ERP API)

Notes:
- Steps 2 and 4 can be parallelized after prerequisites are derived.
- The final WorkOrder must include readiness status and reasons.

Assumption: HR is used only for technician lookup/availability; work orders are created in the ERP system.

## Configuration

Loaded from environment variables:
- Cosmos connection settings (endpoint, database, container, auth)
- HR API base URL + auth
- Part API base URL + auth
- ERP API base URL + auth
- Foundry endpoint/model + auth
- Default timeouts and retry policy knobs

## Logging & Error Handling

### Logging
- Structured logs with correlation id per diagnosed fault
- Log major milestones: mapping found, candidates retrieved, inventory checked, AI plan generated, ERP created
- Redact secrets and avoid logging full prompts/responses by default

### Error Handling
- Use async/await end-to-end
- Use timeouts and retries for network calls
- Surface user-actionable failure reasons:
  - Unknown fault mapping
  - No qualified technician available
  - Parts unavailable/insufficient
  - ERP rejected work order
  - Foundry response invalid

## Testing Strategy

### Unit tests (required)
- `RepairPlanner` behavior: readiness outcomes for common branches
- `AIFoundryService` parsing: valid JSON, missing required fields, malformed JSON
- `CosmosDbService` mapping behavior: known fault, unknown fault, partial mapping

### Integration tests
- Contract validation for HR/Part/ERP adapters using stubbed HTTP responses
- Retry/timeout behavior validation

## Complexity Tracking

No known constitution violations anticipated for this plan.
