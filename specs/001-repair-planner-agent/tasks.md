---

description: "Task list for implementing the Repair Planner Agent"
---

# Tasks: Repair Planner Agent

**Input**: Design documents from `specs/001-repair-planner-agent/`
**Prerequisites**: plan.md (required), spec.md (required for user stories)

**Tests**: Per the constitution, tests are REQUIRED for new/changed logic and for bug fixes. The tasks below include a minimal test project plus targeted unit tests for orchestration and Foundry JSON parsing.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- All tasks include exact file paths in descriptions

## Path Conventions

- Single project layout under `src/` (implementation) and `tests/` (when added)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create solution and .NET 10 console project in RepairPlanner.slnx and src/RepairPlannerAgent/RepairPlannerAgent.csproj
- [x] T002 Add baseline repo docs for the agent in src/RepairPlannerAgent/README.md
- [x] T003 [P] Add .NET formatting and analyzer configuration in .editorconfig
- [x] T004 Add configuration template for env vars in src/RepairPlannerAgent/.env.example
- [x] T005 [P] Create folder structure per plan in src/RepairPlannerAgent/Models/, src/RepairPlannerAgent/Services/, src/RepairPlannerAgent/Planning/, src/RepairPlannerAgent/Contracts/
- [x] T006 Create test project tests/RepairPlannerAgent.Tests/RepairPlannerAgent.Tests.csproj and add it to RepairPlanner.slnx

**Checkpoint**: Repo has the target skeleton and a buildable project.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T007 Add DI + logging + config dependencies in src/RepairPlannerAgent/RepairPlannerAgent.csproj
- [x] T008 Implement options models for external dependencies in src/RepairPlannerAgent/Planning/RepairPlannerOptions.cs
- [x] T009 Implement shared error/result primitives in src/RepairPlannerAgent/Planning/PlanningResult.cs
- [x] T010 Implement models with XML documentation and C# naming conventions in src/RepairPlannerAgent/Models/DiagnosedFault.cs
- [x] T011 [P] Implement models with XML documentation in src/RepairPlannerAgent/Models/Technician.cs
- [x] T012 [P] Implement models with XML documentation in src/RepairPlannerAgent/Models/Part.cs
- [x] T013 [P] Implement models with XML documentation in src/RepairPlannerAgent/Models/SkillRequirement.cs
- [x] T014 [P] Implement work order models (including tasks and part requirements) with XML documentation in src/RepairPlannerAgent/Models/WorkOrder.cs
- [x] T015 [P] Implement work order task model in src/RepairPlannerAgent/Models/WorkOrderTask.cs
- [x] T016 [P] Implement part requirement model in src/RepairPlannerAgent/Models/PartRequirement.cs
- [x] T017 Define integration contract notes placeholders in src/RepairPlannerAgent/Contracts/HrApiContracts.md
- [x] T018 [P] Define integration contract notes placeholders in src/RepairPlannerAgent/Contracts/PartApiContracts.md
- [x] T019 [P] Define integration contract notes placeholders in src/RepairPlannerAgent/Contracts/ErpApiContracts.md
- [x] T020 [P] Define integration contract notes placeholders in src/RepairPlannerAgent/Contracts/FoundryContracts.md
- [X] T021 Implement Program composition root with env config + DI wiring in src/RepairPlannerAgent/Program.cs (without external calls yet)

**Checkpoint**: Foundation ready (models, config, logging, DI) — user story implementation can now begin.

---

## Phase 3: User Story 1 - Generate a Repair Plan from a Diagnosed Fault (Priority: P1) 🎯 MVP

**Goal**: Convert a diagnosed fault into a structured repair plan (tasks, required skills, required parts) even if no technicians/parts are available.

**Independent Test**: Run Program with a sample DiagnosedFault and verify a WorkOrder object is produced containing tasks + required skills + required parts, with a correlation id logged.

### Tests for User Story 1 (write FIRST)

- [X] T022 [P] [US1] Add Foundry JSON parsing unit tests in tests/RepairPlannerAgent.Tests/AIFoundryServiceParsingTests.cs
- [X] T023 [P] [US1] Add mapping fallback unit tests in tests/RepairPlannerAgent.Tests/CosmosDbServiceMappingTests.cs
- [X] T024 [US1] Add RepairPlanner orchestration unit tests in tests/RepairPlannerAgent.Tests/RepairPlannerTests.cs

### Implementation for User Story 1

- [X] T025 [US1] Define repair-mapping document shape used by Cosmos in src/RepairPlannerAgent/Services/RepairMappingDocument.cs
- [X] T026 [US1] Implement CosmosDbService to query required skills/parts by fault type in src/RepairPlannerAgent/Services/CosmosDbService.cs
- [X] T027 [US1] Implement AIFoundryService prompt builder in src/RepairPlannerAgent/Services/AIFoundryService.cs
- [X] T028 [US1] Implement AIFoundryService response parsing to WorkOrder with JSON deserialization error handling in src/RepairPlannerAgent/Services/AIFoundryService.cs
- [X] T029 [US1] Implement RepairPlanner orchestration (skills/parts via Cosmos → AI plan → WorkOrder) in src/RepairPlannerAgent/Planning/RepairPlanner.cs
- [X] T030 [US1] Update Program sample flow to call RepairPlanner and print the resulting WorkOrder summary in src/RepairPlannerAgent/Program.cs
- [X] T031 [US1] Add safety fallback behavior for unknown fault mapping in src/RepairPlannerAgent/Planning/RepairPlanner.cs

**Checkpoint**: User Story 1 works end-to-end locally with a safe fallback path.

---

## Phase 4: User Story 2 - Validate Technician and Parts Prerequisites (Priority: P2)

**Goal**: Select qualified + available technicians and validate parts inventory, producing readiness status and actionable reasons when blocked.

**Independent Test**: Provide a repair plan + mocked HR/Parts responses and verify the WorkOrder is marked ready vs blocked with correct technician recommendation and parts availability results.

### Tests for User Story 2 (write FIRST)

- [ ] T032 [US2] Add readiness branching unit tests in tests/RepairPlannerAgent.Tests/RepairPlannerTests.cs

### Implementation for User Story 2

- [ ] T033 [P] [US2] Implement HRService client (skills query + availability) using HttpClient in src/RepairPlannerAgent/Services/HRService.cs
- [ ] T034 [P] [US2] Implement PartService client (inventory by part numbers) using HttpClient in src/RepairPlannerAgent/Services/PartService.cs
- [ ] T035 [US2] Add prerequisite validation logic (technician selection + parts readiness) in src/RepairPlannerAgent/Planning/RepairPlanner.cs
- [ ] T036 [US2] Add structured logging for prerequisite checks (counts, chosen technician id, missing parts) in src/RepairPlannerAgent/Planning/RepairPlanner.cs
- [ ] T037 [US2] Update WorkOrder model to include readiness reasons and prerequisite results (if missing) in src/RepairPlannerAgent/Models/WorkOrder.cs
- [ ] T038 [US2] Update Program sample to exercise prerequisite validation with sample/mock data paths in src/RepairPlannerAgent/Program.cs

**Checkpoint**: User Story 2 produces “ready/blocked/needs review” with clear reasons.

---

## Phase 5: User Story 3 - Create a Structured ERP Work Order (Priority: P3)

**Goal**: Create an ERP work order when prerequisites are satisfied, returning the created work order reference.

**Independent Test**: For a “ready” plan, verify ERPService is invoked and the returned work order id/reference is attached to the WorkOrder output; for “blocked”, verify ERP is not called.

### Tests for User Story 3 (write FIRST)

- [ ] T039 [US3] Add ERP call gating unit tests in tests/RepairPlannerAgent.Tests/RepairPlannerTests.cs

### Implementation for User Story 3

- [ ] T040 [US3] Implement ERPService client to create work orders using HttpClient in src/RepairPlannerAgent/Services/ERPService.cs
- [ ] T041 [US3] Define ERP request/response DTOs in src/RepairPlannerAgent/Services/ErpWorkOrderRequest.cs and src/RepairPlannerAgent/Services/ErpWorkOrderResponse.cs
- [ ] T042 [US3] Update RepairPlanner to call ERPService only when readiness is ready and to attach ERP id in src/RepairPlannerAgent/Planning/RepairPlanner.cs
- [ ] T043 [US3] Update Program to display ERP work order reference and final status in src/RepairPlannerAgent/Program.cs
- [ ] T044 [US3] Add error handling for ERP failures (retries/timeouts + actionable errors) in src/RepairPlannerAgent/Services/ERPService.cs

**Checkpoint**: A structured ERP work order is created only when prerequisites are satisfied.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T045 [P] Add input/output JSON examples for DiagnosedFault and WorkOrder in src/RepairPlannerAgent/README.md
- [ ] T046 Add end-to-end correlation id propagation and include it in all logs in src/RepairPlannerAgent/Planning/RepairPlanner.cs
- [ ] T047 [P] Harden redaction rules for Foundry prompts/responses in src/RepairPlannerAgent/Services/AIFoundryService.cs
- [ ] T048 Add consistent timeout + retry configuration knobs in src/RepairPlannerAgent/Planning/RepairPlannerOptions.cs
- [ ] T049 Run manual validation steps and document them in specs/001-repair-planner-agent/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3+)**: Depend on Foundational completion
- **Polish (Phase 6)**: Depends on desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2 foundation; produces WorkOrder structure and AI-based plan generation
- **US2 (P2)**: Depends on US1’s WorkOrder structure; adds prerequisites validation via HR + Parts
- **US3 (P3)**: Depends on US2 readiness outcomes; adds ERP creation behavior

### Dependency Graph (high-level)

```text
Phase 1 (Setup)
  -> Phase 2 (Foundational)
      -> US1 (P1)
          -> US2 (P2)
              -> US3 (P3)
                  -> Phase 6 (Polish)
```

---

## Parallel Execution Examples

### US1 Parallel Opportunities

- Implement Cosmos mapping types and Foundry prompt/parsing in parallel:
  - T021 and T023/T024

### US2 Parallel Opportunities

- Implement HTTP adapters in parallel:
  - T028 and T029

### US3 Parallel Opportunities

- Implement ERP client and DTOs in parallel:
  - T034 and T035

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Stop and validate: repair plan generation works for known + unknown faults

### Incremental Delivery

1. Add US1: generate plan from diagnosis
2. Add US2: validate technician + parts prerequisites
3. Add US3: create ERP work order
4. Add Polish: redaction, config hardening, quickstart validation
