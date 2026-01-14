# Feature Specification: Repair Planner Agent

**Feature Branch**: `001-repair-planner-agent`  
**Created**: 2026-01-14  
**Status**: Draft  
**Input**: User description: "Create an intelligent Repair Planner Agent that generates comprehensive repair plans and work orders when faults are detected in tire manufacturing equipment. After a fault has been diagnosed, this agent determines: repair tasks, required skills, qualified+available technician, required parts, parts availability; then creates a structured Work Order in the ERP system."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a Repair Plan from a Diagnosed Fault (Priority: P1)

As a maintenance coordinator, I want an automatically generated repair plan when a fault is diagnosed on tire manufacturing equipment, so that repairs can be started quickly with clear, consistent guidance.

**Why this priority**: This is the core value of the agent: turning diagnosis into actionable repair work.

**Independent Test**: Provide a diagnosed fault input and verify the system produces a structured repair plan containing tasks, required skills, and required parts (even if no technician/parts are available yet).

**Acceptance Scenarios**:

1. **Given** a diagnosed fault containing machine identifier, fault classification, and symptom details, **When** the repair planner runs, **Then** it returns a repair plan containing one or more repair tasks with descriptions, safety notes, and estimated effort.
2. **Given** a diagnosed fault that is unknown or has insufficient detail, **When** the repair planner runs, **Then** it returns a safe fallback plan (e.g., triage/inspection tasks) and clearly indicates what information is missing.

---

### User Story 2 - Validate Technician and Parts Prerequisites (Priority: P2)

As a maintenance coordinator, I want the repair plan to include a recommended technician assignment and a parts readiness check, so that we do not issue work that cannot be started.

**Why this priority**: Scheduling the right technician and ensuring parts availability reduces downtime and prevents stalled work orders.

**Independent Test**: Provide a repair plan plus current technician roster and inventory snapshot and verify the system selects qualified+available technicians (or explains why not) and marks parts as available/unavailable with next steps.

**Acceptance Scenarios**:

1. **Given** a repair plan with required skills and a technician roster with skills and availability, **When** the repair planner validates prerequisites, **Then** it recommends a qualified technician who is available within the required time window.
2. **Given** required parts for the repair plan and an inventory snapshot, **When** the repair planner validates prerequisites, **Then** it identifies whether each part is available in sufficient quantity and highlights shortages.
3. **Given** that no qualified technician is available or critical parts are unavailable, **When** prerequisites are validated, **Then** the repair planner returns a blocked status with actionable options (e.g., next available technician(s), alternative skills coverage, substitute parts where allowed, or escalation).

---

### User Story 3 - Create a Structured ERP Work Order (Priority: P3)

As an operations system, I want a work order created in the ERP only when prerequisites are satisfied (or with explicit exceptions), so that execution tracking is reliable and auditable.

**Why this priority**: Work orders are the source of truth for execution and reporting; correctness and traceability matter.

**Independent Test**: Provide a repair plan with validated prerequisites and verify the system creates a structured work order record with the correct fields and references.

**Acceptance Scenarios**:

1. **Given** a repair plan with a qualified technician assignment and required parts confirmed available, **When** the repair planner submits to the ERP, **Then** a work order is created with tasks, assignment, parts list, and a reference to the originating diagnosed fault.
2. **Given** a repair plan where prerequisites are blocked, **When** the repair planner attempts ERP submission, **Then** no work order is created and the response explains which prerequisite(s) failed.

### Edge Cases

- Multiple faults are diagnosed for the same machine in a short time window.
- Technician availability changes during planning (e.g., reassigned, shift ends).
- Inventory changes during planning (e.g., part reserved by another job).
- A part is available but is not compatible with the specific machine variant.
- The ERP is temporarily unavailable or rejects the work order due to validation rules.

### Integration Boundaries (include when applicable)

- Inputs: diagnosed fault details from the upstream diagnosis component.
- Dependencies: technician roster/skills and availability source; inventory source for parts availability; ERP work order creation interface.
- Outputs: repair plan (tasks/skills/parts + readiness status) and ERP work order reference when created.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a diagnosed fault input that includes machine identifier, timestamp, fault classification, and diagnostic details (including confidence/uncertainty when available).
- **FR-002**: System MUST generate a repair plan consisting of one or more repair tasks relevant to the diagnosed fault.
- **FR-003**: Each repair task MUST include a clear description, expected outcome, and safety considerations when applicable.
- **FR-004**: System MUST determine the skills required to perform each repair task.
- **FR-005**: System MUST determine the parts (including quantity) required to perform the repair tasks.
- **FR-006**: System MUST check technician eligibility by matching required skills to technician skills.
- **FR-007**: System MUST check technician availability and identify at least one recommended technician when available.
- **FR-008**: System MUST check parts availability in inventory and identify shortages for any required parts.
- **FR-009**: System MUST produce an overall readiness status for execution: ready, blocked, or needs review.
- **FR-010**: When readiness is blocked, system MUST provide actionable next steps (e.g., next available technician candidates, estimated wait time, alternative parts where permitted, or escalation).
- **FR-011**: System MUST create a structured work order in the ERP system when readiness is ready (or when a documented exception is explicitly approved).
- **FR-012**: The created work order MUST include: linked machine identifier, linked diagnosed fault reference, tasks, skill requirements, technician assignment (if available), parts list, and priority/severity.
- **FR-013**: System MUST return a structured response that includes either the work order reference (on success) or a clear explanation of why creation did not occur.
- **FR-014**: System MUST capture an audit record of planning inputs, prerequisite checks performed, and final decisions for traceability.

### Key Entities *(include if feature involves data)*

- **Diagnosed Fault**: A confirmed fault event for a specific machine, including classification, symptoms, diagnostic confidence, and timestamp.
- **Repair Task**: An actionable unit of work with required skills, required parts, safety notes, and estimated effort.
- **Skill**: A capability needed to execute tasks (e.g., mechanical, electrical, controls).
- **Technician**: A person who can be assigned work, including skills, current status, and availability.
- **Availability Window**: When a technician is available to start and complete tasks.
- **Part Requirement**: A part identifier and quantity needed for a task or plan.
- **Inventory Item**: Current stock status for a part, including available quantity and constraints (e.g., reserved/on-hold).
- **Work Order**: A structured record in the ERP representing planned work for execution and tracking.

### Assumptions

- A separate component has already performed fault diagnosis and produces a structured diagnosed-fault output.
- Technician data includes both skill coverage and availability (or enough information to infer availability).
- Inventory data can report available quantity and distinguish unavailable vs. reserved/on-hold.
- The ERP supports creating work orders with task lists and parts lists.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For at least 95% of diagnosed faults with sufficient detail, the system produces a repair plan without manual intervention.
- **SC-002**: For at least 90% of generated plans, the system correctly identifies whether prerequisites are ready vs. blocked (as confirmed by operations review).
- **SC-003**: Work orders are created successfully for at least 98% of "ready" plans (excluding external outages).
- **SC-004**: Average time from diagnosis to a prepared work order (or blocked decision with next steps) is under 2 minutes.
- **SC-005**: Maintenance coordinators report improved planning efficiency, with a measurable reduction in manual planning time (target: 30% reduction) within the first month of use.
