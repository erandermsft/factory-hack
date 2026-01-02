"""
Challenge 3 (Refactored) - Predictive Maintenance Scheduler

This package contains a refactored version of the maintenance scheduler agent
with improved modularity, testability, and maintainability.

Structure:
- models/: Data models (WorkOrder, MaintenanceSchedule, etc.)
- services/: Business logic (CosmosDB service, Agent service)
- utils/: Shared utilities (datetime, JSON parsing)
- observability/: Tracing and monitoring setup
- config.py: Configuration management
- maintenance_scheduler.py: Main entry point
"""

__version__ = "2.0.0"
__all__ = []
