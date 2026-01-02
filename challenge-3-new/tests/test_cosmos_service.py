"""Tests for CosmosDB service."""
import pytest
from unittest.mock import Mock, patch, AsyncMock
from datetime import datetime

from services.cosmos_db_service import CosmosDbService
from models import WorkOrder, MaintenanceHistory, MaintenanceWindow


class TestCosmosDbService:
    """Test suite for CosmosDbService."""
    
    @pytest.mark.asyncio
    async def test_get_work_order_success(self):
        """Test successful work order retrieval."""
        # Setup mock
        mock_container = Mock()
        mock_container.query_items.return_value = [{
            "id": "wo-test-001",
            "machineId": "MACHINE-001",
            "faultType": "Hydraulic Pressure Drop",
            "priority": "High",
            "assignedTechnician": "tech-001",
            "requiredParts": [],
            "estimatedDuration": 180,
            "createdAt": "2026-01-02T10:00:00Z",
            "status": "Created"
        }]
        
        with patch.object(CosmosDbService, '__init__', lambda x, y, z, w: None):
            service = CosmosDbService("", "", "")
            service.database = Mock()
            service.database.get_container_client.return_value = mock_container
            
            # Execute
            work_order = await service.get_work_order("wo-test-001")
            
            # Assert
            assert work_order.id == "wo-test-001"
            assert work_order.machine_id == "MACHINE-001"
            assert work_order.fault_type == "Hydraulic Pressure Drop"
    
    @pytest.mark.asyncio
    async def test_get_work_order_not_found(self):
        """Test work order not found raises exception."""
        mock_container = Mock()
        mock_container.query_items.return_value = []
        
        with patch.object(CosmosDbService, '__init__', lambda x, y, z, w: None):
            service = CosmosDbService("", "", "")
            service.database = Mock()
            service.database.get_container_client.return_value = mock_container
            
            # Execute and assert
            with pytest.raises(Exception, match="not found"):
                await service.get_work_order("wo-nonexistent")
    
    @pytest.mark.asyncio
    async def test_get_maintenance_history(self):
        """Test retrieving maintenance history."""
        mock_container = Mock()
        mock_container.query_items.return_value = [
            {
                "id": "hist-001",
                "machineId": "MACHINE-001",
                "faultType": "Hydraulic Pressure Drop",
                "occurrenceDate": "2025-12-15T08:00:00Z",
                "resolutionDate": "2025-12-15T11:30:00Z",
                "downtime": 210,
                "cost": 1500.0
            }
        ]
        
        with patch.object(CosmosDbService, '__init__', lambda x, y, z, w: None):
            service = CosmosDbService("", "", "")
            service.database = Mock()
            service.database.get_container_client.return_value = mock_container
            
            # Execute
            history = await service.get_maintenance_history("MACHINE-001")
            
            # Assert
            assert len(history) == 1
            assert history[0].machine_id == "MACHINE-001"
            assert history[0].downtime == 210
    
    def test_generate_mock_windows(self):
        """Test mock window generation."""
        with patch.object(CosmosDbService, '__init__', lambda x, y, z, w: None):
            service = CosmosDbService("", "", "")
            
            # Execute
            windows = service._generate_mock_windows(7)
            
            # Assert
            assert len(windows) == 7
            assert all(w.production_impact == "Low" for w in windows)
            assert all(w.is_available for w in windows)
