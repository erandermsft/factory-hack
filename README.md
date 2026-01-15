# Intelligent Predictive Maintenance Hackathon

Welcome to the Intelligent Predictive Maintenance Hackathon! 🏭 Today, you'll dive into the world of intelligent agent systems powered by Azure AI to revolutionize equipment maintenance in tire manufacturing. Get ready for a hands-on, high-impact day of learning and innovation!

## Introduction

Get ready to transform maintenance with AI using the revolutionary **Microsoft Agent Framework**! In this hackathon, you'll master the latest enterprise-grade agent technology to build intelligent maintenance systems that detect anomalies, diagnose faults, and schedule repairs—just like real maintenance teams, but faster and more accurate.

Using sequential orchestration and Azure AI integration, your specialized agents will collaborate seamlessly to automate complex maintenance workflows in tire manufacturing. From telemetry monitoring through root cause analysis to work order creation and technician scheduling, you'll create a multi-agent system with comprehensive observability that redefines how factories prevent downtime and optimize operations.

## Learning Objectives 🎯

By participating in this hackathon, you will learn how to:

- **Master Microsoft Agent Framework** using the enterprise-grade SDK for building, orchestrating, and deploying sophisticated AI agents with sequential workflows and multi-agent systems
- **Build Specialized Maintenance Agents** (Anomaly Detection, Fault Diagnosis, Repair Planner, Scheduler) with advanced prompt engineering, tool integration, and persistent memory capabilities
- **Leverage GitHub Copilot for Agent Development** using specialized agents like @agentplanning to accelerate development through AI-driven code generation and architectural guidance
- **Implement Persistent Agent Memory** using Microsoft Foundry's thread-based conversation memory to enable agents to maintain context and learn from historical patterns across multiple sessions
- **Deploy Sequential Orchestration** leveraging Agent Framework workflows to coordinate specialized agents into cohesive maintenance pipelines

## Architecture

In this hackathon we will leverage the **Microsoft Agent Framework** to create a sophisticated, enterprise-ready predictive maintenance solution. The architecture follows a 4-agent sequential pattern:

- **Anomaly Detection Agent:** Monitors IoT telemetry and detects abnormal equipment behavior using threshold-based logic from Azure Cosmos DB
- **Fault Diagnosis Agent:** Performs root cause analysis using RAG pattern with Azure AI Search and Microsoft Foundry agents for intelligent diagnostics
- **Repair Planner Agent:** Creates work orders and validates resource availability (parts, technicians, schedule) using Microsoft Foundry agents and multi-container Cosmos DB queries
- **Maintenance Scheduler Agent:** Coordinates logistics, books technicians, and manages execution using Microsoft Foundry agents and optional Calendar API integration

The workflow follows the principle of **"right tool for the right job"** - using Microsoft Foundry agents for conversational AI capabilities and sequential orchestration. This ensures continuous monitoring, rapid response to anomalies, and comprehensive maintenance planning with full observability.

![Architecture](./images/architecture.png)
[TODO: add marchitecture overview image]


## Challenges

- **Challenge 0**: **[Environment Setup & Data Foundation](challenge-0/challenge-0.md)** : Set up your development environment, deploy Azure resources, configure environment variables, and seed sample factory data with 5 machines including pre-seeded warning conditions. You will also spend a few minutes to be more familiar with the business scenario we are using in this hackathon and the problem we are trying to solve.
- **Challenge 1**: **[Anomaly Detection and Fault Diagnosis Agents](challenge-1/challenge-1.md)**: Build an agent that monitors IoT telemetry from tire manufacturing equipment, compares readings against thresholds, and detects anomalies using threshold-based logic
- **Challenge 2**: **[Repair Planner Agent and AI-Driven Development](challenge-2/challange-2md)**: Learn agent-driven development with GitHub Copilot by using the @agentplanning agent to guide you through building a Repair Planner Agent in .NET
- **Challenge 3**: **[Predictive Maintenance & Parts Ordering Agents with Memory](challenge-3/challenge-3.md)**: Build Predictive Maintenance and Parts Ordering agents using Microsoft Foundry's persistent memory layer to maintain context across sessions
- **Challenge 4**: **[Multi-Agent Orchestration](challenge-4/README.md)**: Create the workflow of these 4 agents using Microsoft Agent Framework and deploy it to an Azure Container App


## Requirements

To successfully complete this hackathon, you will need the following:

- GitHub account to access the repository and run GitHub Codespaces and use Github Copilot
- Be familiar with Python or .NET programming, including handling JSON data and making API calls
- Be familiar with Generative AI Solutions and Azure Services
- An active Azure subscription, with Owner rights
- Ability to provision resources in **Sweden Central** or [another supported region](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/concepts/models?tabs=global-standard%2Cstandard-chat-completions#global-standard-model-availability)



## Contributing

We welcome contributions! Please see the [Contributing Guide](CONTRIBUTING.md) for details on coding standards, development environment setup and submission processes.