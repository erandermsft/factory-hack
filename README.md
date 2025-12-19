# Intelligent Predictive Maintenance Hackathon

Welcome to the Intelligent Predictive Maintenance Hackathon! 🏭 Today, you'll dive into the world of intelligent agent systems powered by Azure AI to revolutionize equipment maintenance in tire manufacturing. Get ready for a hands-on, high-impact day of learning and innovation!

## Introduction

Get ready to transform maintenance with AI using the revolutionary **Microsoft Agent Framework**! In this hackathon, you'll master the latest enterprise-grade agent technology to build intelligent maintenance systems that detect anomalies, diagnose faults, and schedule repairs—just like real maintenance teams, but faster and more accurate.

Using sequential orchestration and Azure AI integration, your specialized agents will collaborate seamlessly to automate complex maintenance workflows in tire manufacturing. From telemetry monitoring through root cause analysis to work order creation and technician scheduling, you'll create a multi-agent system with comprehensive observability that redefines how factories prevent downtime and optimize operations.

## Learning Objectives 🎯

By participating in this hackathon, you will learn how to:

- **Master Microsoft Agent Framework** using the enterprise-grade SDK for building, orchestrating, and deploying sophisticated AI agents with sequential workflows and multi-agent systems
- **Build Specialized Maintenance Agents** (Anomaly Detection, Fault Diagnosis, Repair Planner, Scheduler) with advanced prompt engineering, tool integration, and persistent memory capabilities
- **Implement RAG Patterns** using Azure Cognitive Search for knowledge retrieval and intelligent root cause analysis
- **Deploy Sequential Orchestration** leveraging Agent Framework workflows to coordinate specialized agents into cohesive maintenance pipelines
- **Apply Enterprise Observability** using OpenTelemetry monitoring, Microsoft Foundry tracking, and comprehensive system observability for production-ready agent systems

## Architecture

In this hackathon we will leverage the **Microsoft Agent Framework** to create a sophisticated, enterprise-ready predictive maintenance solution. The architecture follows a 4-agent sequential pattern:

- **Anomaly Detection Agent:** Monitors IoT telemetry and detects abnormal equipment behavior using threshold-based logic from Azure Cosmos DB
- **Fault Diagnosis Agent:** Performs root cause analysis using RAG pattern with Azure Cognitive Search and Microsoft Foundry agents for intelligent diagnostics
- **Repair Planner Agent:** Creates work orders and validates resource availability (parts, technicians, schedule) using Microsoft Foundry agents and multi-container Cosmos DB queries
- **Maintenance Scheduler Agent:** Coordinates logistics, books technicians, and manages execution using Microsoft Foundry agents and optional Calendar API integration

The workflow follows the principle of **"right tool for the right job"** - using Microsoft Foundry agents for conversational AI capabilities and sequential orchestration. This ensures continuous monitoring, rapid response to anomalies, and comprehensive maintenance planning with full observability.

![Architecture](./images/architecture.png)



## Challenges

- **Challenge 00**: **[Environment Setup & Data Foundation](challenge-0/README.md)** : Set up your development environment, deploy Azure resources, configure environment variables, and seed sample factory data with 5 machines including pre-seeded warning conditions
- **Challenge 01**: **[Anomaly Detection Agent](challenge-1/README.md)**: Build an agent that monitors IoT telemetry from tire manufacturing equipment, compares readings against thresholds, and detects anomalies using threshold-based logic
- **Challenge 02**: **[Fault Diagnosis Agent](challenge-2/README.md)**: Build an agent that performs root cause analysis using RAG pattern with Azure Cognitive Search and GPT models, querying knowledge base articles and historical repairs
- **Challenge 03**: **[Repair Planner Agent](challenge-3/README.md)**: Build an agent that creates work orders and validates resource availability across multiple systems (parts inventory, technicians, production schedule)
- **Challenge 04**: **[Multi-Agent Orchestration](challenge-4/README.md)**: Build the Scheduler agent and orchestrate all 4 agents into a sequential workflow using Microsoft Agent Framework with comprehensive observability



## Requirements

To successfully complete this hackathon, you will need the following:

- GitHub account to access the repository and run GitHub Codespaces and Github Copilot
- Be familiar with Python programming, including handling JSON data and making API calls
- Be familiar with Generative AI Solutions and Azure Services
- An active Azure subscription, with Owner rights
- Ability to provision resources in **Sweden Central** or [another supported region](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/concepts/models?tabs=global-standard%2Cstandard-chat-completions#global-standard-model-availability)



## Contributing

We welcome contributions! Please see the [Contributing Guide](CONTRIBUTING.md) for details on coding standards, development environment setup and submission processes.