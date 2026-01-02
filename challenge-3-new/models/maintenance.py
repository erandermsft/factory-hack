"""Maintenance-related data models."""
from dataclasses import dataclass
from datetime import datetime
from typing import Optional


@dataclass
class MaintenanceWindow:
    """Available maintenance window from MES"""
    id: str = ""
    start_time: Optional[datetime] = None
    end_time: Optional[datetime] = None
    production_impact: str = ""
    is_available: bool = True


@dataclass
class MaintenanceSchedule:
    """Predictive maintenance schedule output"""
    id: str = ""
    work_order_id: str = ""
    machine_id: str = ""
    scheduled_date: Optional[datetime] = None
    maintenance_window: Optional[MaintenanceWindow] = None
    risk_score: float = 0.0
    predicted_failure_probability: float = 0.0
    recommended_action: str = ""
    reasoning: str = ""
    created_at: Optional[datetime] = None


@dataclass
class MaintenanceHistory:
    """Historical maintenance record"""
    id: str = ""
    machine_id: str = ""
    fault_type: str = ""
    occurrence_date: Optional[datetime] = None
    resolution_date: Optional[datetime] = None
    downtime: int = 0
    cost: float = 0.0
