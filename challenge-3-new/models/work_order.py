"""Work order data models."""
from dataclasses import dataclass, field
from datetime import datetime
from typing import List, Optional


@dataclass
class RequiredPart:
    """Part required for maintenance"""
    part_number: str = ""
    part_name: str = ""
    quantity: int = 0
    is_available: bool = False


@dataclass
class WorkOrder:
    """Work order from the Repair Planner Agent"""
    id: str = ""
    machine_id: str = ""
    fault_type: str = ""
    priority: str = ""
    assigned_technician: str = ""
    required_parts: List[RequiredPart] = field(default_factory=list)
    estimated_duration: int = 0
    created_at: Optional[datetime] = None
    status: str = "Created"
