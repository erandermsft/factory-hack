"""Tests for scheduler agent."""
import pytest
from unittest.mock import Mock, patch, AsyncMock
from datetime import datetime

from services.scheduler_agent import MaintenanceSchedulerAgent
from models import WorkOrder, MaintenanceHistory, MaintenanceWindow


class TestMaintenanceSchedulerAgent:
    """Test suite for MaintenanceSchedulerAgent."""
    
    def test_build_context(self):
        """Test context building for AI analysis."""
        mock_cosmos = Mock()
        agent = MaintenanceSchedulerAgent(
            "https://test.endpoint",
            "gpt-4o",
            mock_cosmos
        )
        
        work_order = WorkOrder(
            id="wo-test-001",
            machine_id="MACHINE-001",
            fault_type="Hydraulic Pressure Drop",
            priority="High",
            estimated_duration=180
        )
        
        history = [
            MaintenanceHistory(
                id="hist-001",
                machine_id="MACHINE-001",
                fault_type="Hydraulic Pressure Drop",
                occurrence_date=datetime(2025, 12, 15),
                downtime=210,
                cost=1500.0
            )
        ]
        
        windows = [
            MaintenanceWindow(
                id="mw-test-001",
                start_time=datetime(2026, 1, 3, 22, 0),
                end_time=datetime(2026, 1, 4, 6, 0),
                production_impact="Low",
                is_available=True
            )
        ]
        
        # Execute
        context = agent._build_context(work_order, history, windows)
        
        # Assert
        assert "wo-test-001" in context
        assert "MACHINE-001" in context
        assert "Hydraulic Pressure Drop" in context
        assert "High" in context
        assert "180 minutes" in context
        assert "mw-test-001" in context
        assert "low" in context.lower()
    
    def test_build_context_no_history(self):
        """Test context building with no historical data."""
        mock_cosmos = Mock()
        agent = MaintenanceSchedulerAgent(
            "https://test.endpoint",
            "gpt-4o",
            mock_cosmos
        )
        
        work_order = WorkOrder(
            id="wo-test-001",
            machine_id="MACHINE-001",
            fault_type="Hydraulic Pressure Drop",
            priority="High",
            estimated_duration=180
        )
        
        # Execute
        context = agent._build_context(work_order, [], [])
        
        # Assert
        assert "No historical maintenance data available" in context
        assert "No maintenance windows available" in context
