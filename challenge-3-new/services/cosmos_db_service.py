"""Cosmos DB service for data access."""
from datetime import datetime, timedelta
from typing import List
from azure.cosmos import CosmosClient, PartitionKey, exceptions

from models import (
    WorkOrder, RequiredPart,
    MaintenanceWindow, MaintenanceSchedule, MaintenanceHistory
)
from utils import parse_datetime


class CosmosDbService:
    """Service for interacting with Cosmos DB"""
    
    def __init__(self, endpoint: str, key: str, database_name: str):
        """Initialize Cosmos DB service.
        
        Args:
            endpoint: Cosmos DB endpoint URL
            key: Cosmos DB access key
            database_name: Name of the database
        """
        self.client = CosmosClient(endpoint, key)
        self.database = self.client.get_database_client(database_name)
    
    async def get_work_order(self, work_order_id: str) -> WorkOrder:
        """Get work order from ERP system.
        
        Args:
            work_order_id: Work order ID to retrieve
            
        Returns:
            WorkOrder object
            
        Raises:
            Exception: If work order not found
        """
        container = self.database.get_container_client("WorkOrders")
        try:
            query = "SELECT * FROM c WHERE c.id = @id"
            items = list(container.query_items(
                query=query,
                parameters=[{"name": "@id", "value": work_order_id}],
                enable_cross_partition_query=True
            ))
            
            if items:
                item = items[0]
                return WorkOrder(
                    id=item.get('id', ''),
                    machine_id=item.get('machineId', ''),
                    fault_type=item.get('faultType', ''),
                    priority=item.get('priority', ''),
                    assigned_technician=item.get('assignedTechnician', ''),
                    required_parts=[
                        RequiredPart(
                            part_number=p.get('partNumber', ''),
                            part_name=p.get('partName', ''),
                            quantity=p.get('quantity', 0),
                            is_available=p.get('isAvailable', False)
                        ) for p in item.get('requiredParts', [])
                    ],
                    estimated_duration=item.get('estimatedDuration', 0),
                    created_at=parse_datetime(item.get('createdAt')),
                    status=item.get('status', 'Created')
                )
            else:
                raise Exception(f"Work order {work_order_id} not found")
        except exceptions.CosmosHttpResponseError as e:
            raise Exception(f"Work order {work_order_id} not found: {str(e)}")
    
    async def get_maintenance_history(self, machine_id: str) -> List[MaintenanceHistory]:
        """Get historical maintenance records for a machine.
        
        Args:
            machine_id: Machine ID to get history for
            
        Returns:
            List of MaintenanceHistory objects
        """
        try:
            container = self.database.get_container_client("MaintenanceHistory")
            query = "SELECT * FROM c WHERE c.machineId = @machineId ORDER BY c.occurrenceDate DESC"
            items = list(container.query_items(
                query=query,
                parameters=[{"name": "@machineId", "value": machine_id}],
                enable_cross_partition_query=True
            ))
            
            results = []
            for item in items:
                results.append(MaintenanceHistory(
                    id=item.get('id', ''),
                    machine_id=item.get('machineId', ''),
                    fault_type=item.get('faultType', ''),
                    occurrence_date=parse_datetime(item.get('occurrenceDate')),
                    resolution_date=parse_datetime(item.get('resolutionDate')),
                    downtime=item.get('downtime', 0),
                    cost=item.get('cost', 0.0)
                ))
            
            return results
        except Exception as e:
            print(f"Warning: Could not retrieve maintenance history: {str(e)}")
            return []
    
    async def get_available_maintenance_windows(self, days_ahead: int = 14) -> List[MaintenanceWindow]:
        """Get available maintenance windows from MES.
        
        Args:
            days_ahead: Number of days ahead to look for windows
            
        Returns:
            List of MaintenanceWindow objects
        """
        try:
            container = self.database.get_container_client("MaintenanceWindows")
            start_date = datetime.utcnow()
            end_date = start_date + timedelta(days=days_ahead)
            
            query = """SELECT * FROM c 
                      WHERE c.startTime >= @startDate 
                      AND c.startTime <= @endDate 
                      AND c.isAvailable = true 
                      ORDER BY c.startTime"""
            
            items = list(container.query_items(
                query=query,
                parameters=[
                    {"name": "@startDate", "value": start_date.isoformat()},
                    {"name": "@endDate", "value": end_date.isoformat()}
                ],
                enable_cross_partition_query=True
            ))
            
            results = []
            for item in items:
                results.append(MaintenanceWindow(
                    id=item.get('id', ''),
                    start_time=parse_datetime(item.get('startTime')),
                    end_time=parse_datetime(item.get('endTime')),
                    production_impact=item.get('productionImpact', ''),
                    is_available=item.get('isAvailable', True)
                ))
            
            return results if results else self._generate_mock_windows(days_ahead)
        except Exception as e:
            print(f"Warning: Could not retrieve maintenance windows: {str(e)}")
            return self._generate_mock_windows(days_ahead)
    
    def _generate_mock_windows(self, days_ahead: int) -> List[MaintenanceWindow]:
        """Generate mock maintenance windows for testing.
        
        Args:
            days_ahead: Number of days ahead to generate windows
            
        Returns:
            List of mock MaintenanceWindow objects
        """
        windows = []
        start_date = datetime.utcnow().replace(hour=0, minute=0, second=0, microsecond=0) + timedelta(days=1)
        
        for i in range(days_ahead):
            current_date = start_date + timedelta(days=i)
            windows.append(MaintenanceWindow(
                id=f"mw-{current_date.strftime('%Y-%m-%d')}-night",
                start_time=current_date.replace(hour=22),
                end_time=current_date.replace(hour=23, minute=59) + timedelta(hours=6, minutes=1),
                is_available=True,
                production_impact="Low"
            ))
        
        return windows
    
    async def save_maintenance_schedule(self, schedule: MaintenanceSchedule) -> MaintenanceSchedule:
        """Save maintenance schedule to database.
        
        Args:
            schedule: MaintenanceSchedule object to save
            
        Returns:
            Saved MaintenanceSchedule object
        """
        try:
            container = self.database.get_container_client("MaintenanceSchedules")
        except Exception:
            self.database.create_container(id="MaintenanceSchedules", partition_key=PartitionKey(path="/id"))
            container = self.database.get_container_client("MaintenanceSchedules")
        
        item = {
            "id": schedule.id,
            "workOrderId": schedule.work_order_id,
            "machineId": schedule.machine_id,
            "scheduledDate": schedule.scheduled_date.isoformat() if schedule.scheduled_date else None,
            "maintenanceWindow": {
                "id": schedule.maintenance_window.id,
                "startTime": schedule.maintenance_window.start_time.isoformat() if schedule.maintenance_window.start_time else None,
                "endTime": schedule.maintenance_window.end_time.isoformat() if schedule.maintenance_window.end_time else None,
                "productionImpact": schedule.maintenance_window.production_impact,
                "isAvailable": schedule.maintenance_window.is_available
            } if schedule.maintenance_window else None,
            "riskScore": schedule.risk_score,
            "predictedFailureProbability": schedule.predicted_failure_probability,
            "recommendedAction": schedule.recommended_action,
            "reasoning": schedule.reasoning,
            "createdAt": schedule.created_at.isoformat() if schedule.created_at else None
        }
        
        container.create_item(body=item)
        return schedule
    
    async def update_work_order_status(self, work_order_id: str, status: str):
        """Update work order status.
        
        Args:
            work_order_id: Work order ID to update
            status: New status value
        """
        container = self.database.get_container_client("WorkOrders")
        work_order = await self.get_work_order(work_order_id)
        old_status = work_order.status
        
        # Delete old item (partition key is status)
        container.delete_item(item=work_order_id, partition_key=old_status)
        
        # Create updated item
        item = {
            "id": work_order.id,
            "machineId": work_order.machine_id,
            "faultType": work_order.fault_type,
            "priority": work_order.priority,
            "assignedTechnician": work_order.assigned_technician,
            "requiredParts": [
                {
                    "partNumber": p.part_number,
                    "partName": p.part_name,
                    "quantity": p.quantity,
                    "isAvailable": p.is_available
                } for p in work_order.required_parts
            ],
            "estimatedDuration": work_order.estimated_duration,
            "createdAt": work_order.created_at.isoformat() if work_order.created_at else None,
            "status": status
        }
        
        container.upsert_item(body=item)
    
    async def get_machine_chat_history(self, machine_id: str) -> str | None:
        """Get chat history for a machine.
        
        Args:
            machine_id: Machine ID to get chat history for
            
        Returns:
            Chat history JSON string or None if not found
        """
        try:
            container = self.database.get_container_client("ChatHistories")
            item = container.read_item(item=machine_id, partition_key=machine_id)
            return item.get('historyJson')
        except exceptions.CosmosResourceNotFoundError:
            return None
        except Exception:
            return None
    
    async def save_machine_chat_history(self, machine_id: str, history_json: str):
        """Save chat history for a machine.
        
        Args:
            machine_id: Machine ID to save chat history for
            history_json: Chat history as JSON string
        """
        try:
            container = self.database.get_container_client("ChatHistories")
        except Exception:
            self.database.create_container(id="ChatHistories", partition_key=PartitionKey(path="/entityId"))
            container = self.database.get_container_client("ChatHistories")
        
        item = {
            "id": machine_id,
            "entityId": machine_id,
            "entityType": "machine",
            "historyJson": history_json,
            "purpose": "predictive_maintenance",
            "updatedAt": datetime.utcnow().isoformat()
        }
        
        container.upsert_item(body=item)
