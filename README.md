# Factory Ops Hack

Welcome to the Factory Ops Hack! This repository contains a series of challenges designed to test and improve your skills in factory operations, automation, and systems optimization.

## Overview

This hackathon focuses on solving real-world factory operational challenges through code. Each challenge builds upon practical scenarios that factory operators and engineers face daily.

## Structure

The repository is organized into individual challenge folders:

```
challenges/
├── challenge-01/
├── challenge-02/
├── challenge-03/
├── challenge-04/
├── challenge-05/
└── challenge-06/
```

Each challenge folder contains:
- `README.md` - Challenge description and requirements
- `starter/` - Starter code and templates
- `solution/` - Reference solution (hidden until completion)
- `tests/` - Test cases to validate your solution

## Getting Started

1. Clone this repository
2. Navigate to a challenge folder
3. Read the challenge README
4. Implement your solution
5. Run the tests to verify your work

## Challenges

- **Challenge 01: Line Flow Balancing** - Build an agent that balances workload across production stations in real-time, maintaining takt time and detecting bottlenecks.

- **Challenge 02: Materials Kitting & Just-in-Time Delivery** - Create an agent that prepares parts kits, handles substitutions, and coordinates AGV deliveries to the production line.

- **Challenge 03: Changeover & Setup Optimization** - Develop an agent that schedules equipment changeovers optimally, minimizing downtime while ensuring tools and operators are ready.

- **Challenge 04: Energy Pacing & Utilities Management** - Design an agent that optimizes energy consumption by shifting loads to off-peak hours while maintaining production targets.

- **Challenge 05: Multi-Agent Orchestration & Exception Recovery** - Build the master orchestrator that coordinates all specialist agents and handles factory-wide exceptions with human-in-the-loop approval.

- **Challenge 06: Governance, Observability & Compliance** - Implement the monitoring, tracing, evaluation, and compliance layer that ensures all agents operate safely, efficiently, and within regulatory boundaries.

## Work in Progress

### Technical Architecture Gaps

The following elements from the target architecture need to be integrated into the challenges:

#### 1. Platform & Infrastructure
- **Foundry Agent Service**: All agents should be built on this platform
- **Azure MCP Integration**: Each agent requires Azure Model Context Protocol setup
- **AI Models**: Specific model assignments per agent:
  - Line Flow Orchestrator: Phi-4
  - Changeover & Setup Planner: GPT5
  - Materials Kitting & Delivery: o3
  - Energy Pacing & Utilities: o3-mini
  - Exception & Recovery: Grok 4

#### 2. Agent Capabilities
- **Memory Component**: Each agent needs state persistence and learning capabilities
- **Recovery Playbooks**: Exception handling should use reusable playbook patterns (not ad-hoc responses)

#### 3. Data Systems Integration
Make explicit connections to factory systems:
- **MES (Manufacturing Execution System)**: Orders, routing, yields
- **APS/WFM**: Machine states and advanced planning
- **IoT Sensors**: Energy usage and real-time monitoring
- **WMS/AGF**: Warehouse management and AGV fleet coordination
- **Supplier Data**: External supply chain feeds

#### 4. Governance & Observability Layer
Missing operational capabilities that should be added:
- **App Insights**: Application performance monitoring
- **Tracing & Monitoring**: End-to-end agent execution tracking
- **Evaluations**: Agent performance assessment and benchmarking
- **Safety & Compliance**: Regulatory and safety constraint enforcement
- **Identity Management**: Security and access control

#### 5. Potential Additional Challenges
- **Challenge 00 (Foundation)**: Set up Azure MCP, Foundry Agent Service, and integrate with MES/IoT data sources

### Next Steps
- Update individual challenge READMEs with technical requirements
- Create starter templates for Foundry Agent Service
- Add data integration specifications
- Develop governance and monitoring guidelines

## Contributing

Please read the contribution guidelines before submitting pull requests.

## License

See LICENSE file for details.