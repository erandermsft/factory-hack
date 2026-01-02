"""Sample test data fixtures."""
import json
from pathlib import Path


def load_fixture(filename: str) -> dict:
    """Load a JSON fixture file.
    
    Args:
        filename: Name of the fixture file (without .json extension)
        
    Returns:
        Parsed JSON data
    """
    fixture_path = Path(__file__).parent / f"{filename}.json"
    with open(fixture_path, 'r') as f:
        return json.load(f)


def get_sample_work_order():
    """Get a sample work order for testing."""
    return {
        "id": "wo-test-001",
        "machineId": "MACHINE-001",
        "faultType": "Hydraulic Pressure Drop",
        "priority": "High",
        "assignedTechnician": "tech-001",
        "requiredParts": [
            {
                "partNumber": "HYD-SEAL-100",
                "partName": "Hydraulic Seal",
                "quantity": 2,
                "isAvailable": True
            }
        ],
        "estimatedDuration": 180,
        "createdAt": "2026-01-02T10:00:00Z",
        "status": "Created"
    }


def get_sample_maintenance_history():
    """Get sample maintenance history for testing."""
    return [
        {
            "id": "hist-001",
            "machineId": "MACHINE-001",
            "faultType": "Hydraulic Pressure Drop",
            "occurrenceDate": "2025-12-15T08:00:00Z",
            "resolutionDate": "2025-12-15T11:30:00Z",
            "downtime": 210,
            "cost": 1500.0
        },
        {
            "id": "hist-002",
            "machineId": "MACHINE-001",
            "faultType": "Hydraulic Pressure Drop",
            "occurrenceDate": "2025-11-20T14:00:00Z",
            "resolutionDate": "2025-11-20T17:00:00Z",
            "downtime": 180,
            "cost": 1200.0
        }
    ]


def get_sample_maintenance_windows():
    """Get sample maintenance windows for testing."""
    return [
        {
            "id": "mw-2026-01-03-night",
            "startTime": "2026-01-03T22:00:00Z",
            "endTime": "2026-01-04T06:00:00Z",
            "productionImpact": "Low",
            "isAvailable": True
        },
        {
            "id": "mw-2026-01-04-night",
            "startTime": "2026-01-04T22:00:00Z",
            "endTime": "2026-01-05T06:00:00Z",
            "productionImpact": "Low",
            "isAvailable": True
        }
    ]
