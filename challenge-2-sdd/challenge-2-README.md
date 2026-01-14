# Challenge 2: Building the Repair Planner Agent with GitHub Copilot

**Expected Duration**: 30 min

Welcome to Challenge 2!

In this challenge...


## Step 1: Understand spec driven development

Establish project principles

```
Create principles focused on code quality, modularity and integration requirements. Include governance for how these principles should guide technical decisions and implementation choices.
```

Create the spec

```
Create an intelligent Repair Planner Agent that generates comprehensive repair plans and work orders when faults are detected in tire manufacturing equipment.

The Repair Planner Agent is the third component in a multi-agent system. After a fault has been diagnosed, this agent determines:

- What repair tasks need to be performed
- Which skills are needed to perform the repair
- Which technician has the required skills and is the technician available
- What parts are needed to perform the repair 
- Are the required parts available in the inventory

After validating the prerequisites the Repair Planner agent creates a structured Work Order in the ERP system

```

Create a technical implementation plan

```
Create the intelligent Repair Planner Agent using .NET 10. 

The data models needed for the Repair Planner Agent:
- DiagnosedFault (input from previous agent)
- Technician (with skills and availability)
- Part (inventory items)
- WorkOrder (output with tasks and resources)

Use proper C# naming conventions and add XML documentation.

Create a CosmosDbService class that:
- Queries technicians by required skills
- Queries what parts are needed for a specific repair
- Creates work orders in Cosmos DB
- Creates work orders 

Create a FaultMappingService class that:
- Returns a mapping what skills are needed to repair a particular fault 
- Returns a mapping what parts are needed to repair a particular fault

Include error handling, logging, and async patterns.

Create an AIFoundryService that uses Microsoft Foundry to generate 
repair plans. The service should:
- Accept a diagnosed fault, available technicians, and parts
- Build a structured prompt for the LLM
- Parse the response into a WorkOrder object
- Handle JSON deserialization errors

Create the main RepairPlanner class that orchestrates:
1. Determining required skills from fault type
2. Querying available technicians based on skills needed
3. Determine what parts are needed based on the fault type
4. Checking parts inventory
5. Generating the repair plan with AI
6. Saving the work order to HR API 

Include comprehensive logging and error handling.

Create a Program.cs that:
- Loads configuration from environment variables
- Initializes all services with dependency injection
- Creates a sample diagnosed fault
- Calls the repair planner
- Displays the work order results

```


Break down into tasks

Create implementation

## Step 2: Initialize Specify CLI 

```bash
cd challenge-2-sdd

# Initialize a new Specify project but don't create a new branch 
specify init RepairPlanner --no-git 

```
In the prompts select `GitHub Copilot` as the agent and `sh` as the script type 


Copy starter files and open the specs in a new code session

``` bash
cp /docs/constitution /RepairPlanner/.specify/memory/constitution.md
cp /docs/spec.md /RepairPlanner/...

cd RepairPlanner
# open the spec project in a new window to make RepairPlanner folder the root and to isolate from other files in this repo 
code .
...










