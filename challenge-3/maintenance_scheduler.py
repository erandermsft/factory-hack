#!/usr/bin/env python3
"""
Maintenance Scheduler Agent - Predictive maintenance scheduling using Microsoft Agent Framework.

This agent finds optimal maintenance windows that minimize production disruption while 
ensuring equipment reliability. It analyzes production schedules, resource availability,
and operational constraints to recommend perfect timing for maintenance activities.

Usage:
    python maintenance_scheduler.py [WORK_ORDER_ID]
    
Example:
    python maintenance_scheduler.py wo-2024-468
"""
import os
import sys
import json
import asyncio
import logging
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from typing import List, Optional, Annotated
from azure.cosmos import CosmosClient, PartitionKey, exceptions
from azure.identity.aio import DefaultAzureCredential
from azure.ai.projects.aio import AIProjectClient
from agent_framework import ChatAgent
from agent_framework_azure_ai import AzureAIAgentClient
from pydantic import Field
from dotenv import load_dotenv

# Azure AI Tracing with Agent Framework
try:
    from agent_framework.observability import configure_otel_providers, enable_instrumentation
    from azure.monitor.opentelemetry.exporter import AzureMonitorTraceExporter, AzureMonitorMetricExporter, AzureMonitorLogExporter
    TRACING_AVAILABLE = True
except ImportError:
    TRACING_AVAILABLE = False
    print("⚠️  Agent Framework observability not available.")

logger = logging.getLogger(__name__)
load_dotenv(override=True)


# =============================================================================
# Data Models
# =============================================================================

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


# =============================================================================
# Cosmos DB Service
# =============================================================================

class CosmosDbService:
    """Service for interacting with Cosmos DB"""
    
    def __init__(self, endpoint: str, key: str, database_name: str):
        self.client = CosmosClient(endpoint, key)
        self.database = self.client.get_database_client(database_name)
    
    def _parse_datetime(self, dt_str):
        """Parse datetime from ISO string"""
        if isinstance(dt_str, datetime):
            return dt_str
        if not dt_str:
            return None
        try:
            return datetime.fromisoformat(dt_str.replace('Z', '+00:00'))
        except:
            return None
    
    async def get_work_order(self, work_order_id: str) -> WorkOrder:
        """Get work order from ERP system"""
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
                    created_at=self._parse_datetime(item.get('createdAt')),
                    status=item.get('status', 'Created')
                )
            else:
                raise Exception(f"Work order {work_order_id} not found")
        except exceptions.CosmosHttpResponseError as e:
            raise Exception(f"Work order {work_order_id} not found: {str(e)}")
    
    async def get_maintenance_history(self, machine_id: str) -> List[MaintenanceHistory]:
        """Get historical maintenance records for a machine"""
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
                    occurrence_date=self._parse_datetime(item.get('occurrenceDate')),
                    resolution_date=self._parse_datetime(item.get('resolutionDate')),
                    downtime=item.get('downtime', 0),
                    cost=item.get('cost', 0.0)
                ))
            
            return results
        except Exception as e:
            print(f"Warning: Could not retrieve maintenance history: {str(e)}")
            return []
    
    async def get_available_maintenance_windows(self, days_ahead: int = 14) -> List[MaintenanceWindow]:
        """Get available maintenance windows from MES"""
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
                    start_time=self._parse_datetime(item.get('startTime')),
                    end_time=self._parse_datetime(item.get('endTime')),
                    production_impact=item.get('productionImpact', ''),
                    is_available=item.get('isAvailable', True)
                ))
            
            return results if results else self._generate_mock_windows(days_ahead)
        except Exception as e:
            print(f"Warning: Could not retrieve maintenance windows: {str(e)}")
            return self._generate_mock_windows(days_ahead)
    
    def _generate_mock_windows(self, days_ahead: int) -> List[MaintenanceWindow]:
        """Generate mock maintenance windows"""
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
        """Save maintenance schedule to database"""
        try:
            container = self.database.get_container_client("MaintenanceSchedules")
        except:
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
        """Update work order status"""
        container = self.database.get_container_client("WorkOrders")
        work_order = await self.get_work_order(work_order_id)
        old_status = work_order.status
        
        container.delete_item(item=work_order_id, partition_key=old_status)
        
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
    
    async def get_machine_chat_history(self, machine_id: str) -> Optional[str]:
        """Get chat history for a machine"""
        try:
            container = self.database.get_container_client("ChatHistories")
            item = container.read_item(item=machine_id, partition_key=machine_id)
            return item.get('historyJson')
        except exceptions.CosmosResourceNotFoundError:
            return None
        except Exception:
            return None
    
    async def save_machine_chat_history(self, machine_id: str, history_json: str):
        """Save chat history for a machine"""
        try:
            container = self.database.get_container_client("ChatHistories")
        except:
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


# =============================================================================
# Agent Service
# =============================================================================

class MaintenanceSchedulerAgent:
    """AI Agent for predictive maintenance scheduling"""
    
    def __init__(self, project_endpoint: str, deployment_name: str, cosmos_service: CosmosDbService):
        self.project_endpoint = project_endpoint
        self.deployment_name = deployment_name
        self.cosmos_service = cosmos_service
    
    async def predict_schedule(
        self,
        work_order: WorkOrder,
        history: List[MaintenanceHistory],
        windows: List[MaintenanceWindow]
    ) -> MaintenanceSchedule:
        """Predict optimal maintenance schedule using AI"""
        
        context = self._build_context(work_order, history, windows)
        chat_history_json = await self.cosmos_service.get_machine_chat_history(work_order.machine_id)
        print(f"   Using persistent chat history for machine: {work_order.machine_id}")
        
        instructions = """You are a predictive maintenance expert specializing in industrial tire manufacturing equipment. 
        
Analyze historical maintenance data and recommend optimal maintenance schedules based on:
1. Historical failure patterns
2. Risk scores (time since last maintenance, fault frequency, downtime costs, criticality)
3. Optimal maintenance windows considering production impact
4. Detailed reasoning

Always respond in valid JSON format as requested."""
        
        credential = DefaultAzureCredential()
        
        async with ChatAgent(
            chat_client=AzureAIAgentClient(
                project_endpoint=self.project_endpoint,
                model_deployment_name=self.deployment_name,
                credential=credential,
                agent_name=f"MaintenanceScheduler-{work_order.machine_id}",
                should_cleanup_agent=False,  # Keep agent visible in portal
            ),
            instructions=instructions,
        ) as agent:
            thread = agent.get_new_thread()
            
            if chat_history_json:
                try:
                    for msg in json.loads(chat_history_json):
                        await thread.add_message(role=msg["role"], content=msg["content"])
                except Exception as e:
                    print(f"   Warning: Could not restore chat history: {e}")
            
            result = await agent.run(context, thread=thread)
            response_text = result.text
            
            await self._save_thread_history(work_order.machine_id, thread)
        
        json_response = self._extract_json(response_text)
        data = json.loads(json_response)
        
        return MaintenanceSchedule(
            id=f"sched-{datetime.utcnow().timestamp()}",
            work_order_id=work_order.id,
            machine_id=work_order.machine_id,
            scheduled_date=datetime.fromisoformat(data['scheduledDate'].replace('Z', '+00:00')),
            maintenance_window=MaintenanceWindow(
                id=data['maintenanceWindow']['id'],
                start_time=datetime.fromisoformat(data['maintenanceWindow']['startTime'].replace('Z', '+00:00')),
                end_time=datetime.fromisoformat(data['maintenanceWindow']['endTime'].replace('Z', '+00:00')),
                production_impact=data['maintenanceWindow']['productionImpact'],
                is_available=data['maintenanceWindow']['isAvailable']
            ),
            risk_score=data['riskScore'],
            predicted_failure_probability=data['predictedFailureProbability'],
            recommended_action=data['recommendedAction'],
            reasoning=data['reasoning'],
            created_at=datetime.utcnow()
        )
    
    async def _save_thread_history(self, machine_id: str, thread):
        """Save thread history to Cosmos DB"""
        try:
            messages = []
            async for msg in thread.list_messages():
                messages.append({
                    "role": msg.role,
                    "content": msg.content[0].text if msg.content and hasattr(msg.content[0], 'text') else str(msg.content)
                })
                if len(messages) >= 10:
                    break
            
            messages.reverse()
            await self.cosmos_service.save_machine_chat_history(machine_id, json.dumps(messages))
        except Exception as e:
            print(f"   Warning: Could not save chat history: {e}")
    
    def _build_context(self, work_order: WorkOrder, history: List[MaintenanceHistory], windows: List[MaintenanceWindow]) -> str:
        """Build analysis context for AI"""
        lines = [
            "# Predictive Maintenance Analysis Request",
            "",
            "## Work Order Information",
            f"- Work Order ID: {work_order.id}",
            f"- Machine ID: {work_order.machine_id}",
            f"- Fault Type: {work_order.fault_type}",
            f"- Priority: {work_order.priority}",
            f"- Estimated Duration: {work_order.estimated_duration} minutes",
            "",
            "## Historical Maintenance Data"
        ]
        
        if history:
            lines.append(f"Total maintenance events: {len(history)}")
            lines.append("")
            
            relevant_history = [h for h in history if h.fault_type == work_order.fault_type]
            if relevant_history:
                lines.append(f"**Similar fault type ({work_order.fault_type}):**")
                lines.append(f"- Occurrences: {len(relevant_history)}")
                avg_downtime = sum(h.downtime for h in relevant_history) / len(relevant_history)
                avg_cost = sum(h.cost for h in relevant_history) / len(relevant_history)
                lines.append(f"- Average downtime: {avg_downtime:.0f} minutes")
                lines.append(f"- Average cost: ${avg_cost:.2f}")
                
                if len(relevant_history) >= 2:
                    dates = sorted([h.occurrence_date for h in relevant_history if h.occurrence_date])
                    if len(dates) >= 2:
                        intervals = [(dates[i] - dates[i-1]).days for i in range(1, len(dates))]
                        avg_interval = sum(intervals) / len(intervals)
                        lines.append(f"- Mean Time Between Failures (MTBF): {avg_interval:.0f} days")
                        
                        last_occurrence = max(h.occurrence_date for h in relevant_history if h.occurrence_date)
                        days_since_last = (datetime.utcnow() - last_occurrence).days
                        lines.append(f"- Days since last occurrence: {days_since_last:.0f}")
                        lines.append(f"- Failure cycle progress: {(days_since_last / avg_interval * 100):.1f}%")
            else:
                lines.append(f"**No previous occurrences of {work_order.fault_type} fault type.**")
            
            lines.append("")
            lines.append("**Recent maintenance events (all types):**")
            for record in history[:5]:
                if record.occurrence_date:
                    lines.append(f"- {record.occurrence_date.strftime('%Y-%m-%d')}: {record.fault_type} ({record.downtime}min, ${record.cost})")
        else:
            lines.append("⚠️  No historical maintenance data available.")
            lines.append("Risk assessment will be based on fault type and priority only.")
        
        lines.extend([
            "",
            "## Available Maintenance Windows (Next 14 Days)"
        ])
        
        if windows:
            for window in windows[:10]:
                if window.start_time and window.end_time:
                    duration = (window.end_time - window.start_time).total_seconds() / 3600
                    lines.append(f"- **{window.start_time.strftime('%Y-%m-%d %H:%M')} to {window.end_time.strftime('%H:%M')}** ({duration:.1f}h)")
                    lines.append(f"  * Production Impact: {window.production_impact}")
                    lines.append(f"  * Window ID: {window.id}")
        else:
            lines.append("⚠️  No maintenance windows available!")
        
        lines.extend([
            "",
            "## Analysis Required",
            "Please provide a JSON response with:",
            "1. Risk score (0-100): Priority base + MTBF progress + historical impact",
            "2. Failure probability (0.0-1.0)",
            "3. Optimal maintenance window selection",
            "4. Recommended action: IMMEDIATE, URGENT, or SCHEDULED",
            "5. Detailed reasoning",
            "",
            "```json",
            "{",
            '  "scheduledDate": "<ISO datetime>",',
            '  "maintenanceWindow": {',
            '    "id": "<window ID>",',
            '    "startTime": "<ISO datetime>",',
            '    "endTime": "<ISO datetime>",',
            '    "productionImpact": "<Low|Medium|High>",',
            '    "isAvailable": true',
            '  },',
            '  "riskScore": <0-100>,',
            '  "predictedFailureProbability": <0.0-1.0>,',
            '  "recommendedAction": "<IMMEDIATE|URGENT|SCHEDULED>",',
            '  "reasoning": "<detailed explanation>"',
            "}",
            "```"
        ])
        
        return "\n".join(lines)
    
    def _extract_json(self, response: str) -> str:
        """Extract JSON from response"""
        if "```json" in response:
            start = response.index("```json") + 7
            end = response.index("```", start)
            return response[start:end].strip()
        
        start = response.find('{')
        if start >= 0:
            end = response.rfind('}')
            return response[start:end+1]
        
        raise Exception("Could not extract JSON from agent response")


# =============================================================================
# Main Program
# =============================================================================

async def main():
    """Main program"""
    print("=== Predictive Maintenance Agent ===\n")
    
    # Load configuration
    cosmos_endpoint = os.getenv("COSMOS_ENDPOINT")
    cosmos_key = os.getenv("COSMOS_KEY")
    database_name = os.getenv("COSMOS_DATABASE_NAME")
    foundry_project_endpoint = os.getenv("FOUNDRY_PROJECT_ENDPOINT")
    deployment_name = os.getenv("FOUNDRY_MODEL_DEPLOYMENT_NAME", "gpt-4o")
    app_insights_connection = os.getenv("APPLICATIONINSIGHTS_CONNECTION_STRING")
    
    # Validate
    if not all([cosmos_endpoint, cosmos_key, database_name, foundry_project_endpoint]):
        print("Error: Missing required environment variables.")
        print("Required: COSMOS_ENDPOINT, COSMOS_KEY, COSMOS_DATABASE_NAME, FOUNDRY_PROJECT_ENDPOINT")
        return
    
    # Enable Azure AI Tracing with Agent Framework
    if TRACING_AVAILABLE and app_insights_connection:
        try:
            # Configure OpenTelemetry with Azure Monitor exporters
            # This sends traces directly to Application Insights/Azure AI Foundry
            trace_exporter = AzureMonitorTraceExporter.from_connection_string(app_insights_connection)
            metric_exporter = AzureMonitorMetricExporter.from_connection_string(app_insights_connection)
            log_exporter = AzureMonitorLogExporter.from_connection_string(app_insights_connection)
            
            configure_otel_providers(
                enable_sensitive_data=True,  # Capture prompts and completions
                exporters=[trace_exporter, metric_exporter, log_exporter]
            )
            print("📊 Agent Framework tracing enabled (Azure Monitor)")
            print(f"   Traces sent to: {app_insights_connection.split(';')[0]}")
            print("   View in Azure AI Foundry portal: https://ai.azure.com -> Your Project -> Tracing\n")
        except Exception as e:
            print(f"⚠️  Tracing setup failed: {e}\n")
    elif TRACING_AVAILABLE:
        print("⚠️  Tracing available but APPLICATIONINSIGHTS_CONNECTION_STRING not set\n")
    
    # Initialize
    cosmos_service = CosmosDbService(cosmos_endpoint, cosmos_key, database_name)
    
    # Register agent in Azure AI Foundry portal
    async with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=foundry_project_endpoint, credential=credential) as project_client,
    ):
        try:
            from azure.ai.projects.models import PromptAgentDefinition
            
            # Get current agent version from portal
            print("   Checking existing agent versions in portal...")
            version_count = 0
            try:
                async for version_obj in project_client.agents.list_versions(agent_name="MaintenanceSchedulerAgent"):
                    version_count += 1
                print(f"   Found {version_count} existing versions")
            except Exception as e:
                print(f"   Error checking versions: {e}")
            
            print(f"   Creating new version (will be version #{version_count + 1})...")
            
            # Create agent definition
            definition = PromptAgentDefinition(
                model=deployment_name,
                instructions="""You are a Predictive Maintenance Scheduler for industrial tire manufacturing equipment.

Analyze work orders, historical maintenance data, and available maintenance windows to:
1. Assess equipment failure risk based on historical patterns and work order priority
2. Identify optimal maintenance windows that minimize production disruption
3. Generate predictive maintenance schedules with risk scores and recommendations

Consider factors like:
- Work order priority (high/medium/low)
- Historical maintenance frequency and patterns
- Production impact of maintenance windows
- Equipment estimated repair duration

Output JSON with: scheduled_date, risk_score (0-100), predicted_failure_probability (0-1), recommended_action (IMMEDIATE/URGENT/SCHEDULED/MONITOR), and reasoning.""",
            )
            
            # Create new version - Azure auto-assigns version number
            print(f"   Registering MaintenanceSchedulerAgent in Azure AI Foundry portal...")
            registered_agent = await project_client.agents.create_version(
                agent_name="MaintenanceSchedulerAgent",
                definition=definition,
                description=f"Predictive maintenance scheduling agent",
                metadata={
                    "framework": "agent-framework",
                    "purpose": "maintenance_scheduling",
                    "timestamp": datetime.utcnow().isoformat()
                }
            )
            print(f"   ✅ New version created!")
            print(f"      Agent ID: {registered_agent.id if hasattr(registered_agent, 'id') else 'N/A'}")
            
            # Verify it was created
            print(f"   Verifying creation...")
            verify_count = 0
            async for v in project_client.agents.list_versions(agent_name="MaintenanceSchedulerAgent"):
                verify_count += 1
            print(f"   Total versions now in portal: {verify_count}")
            print(f"   Check portal at: https://ai.azure.com\n")
        except Exception as e:
            print(f"   ⚠️  Could not register agent in portal: {e}\n")
            logger.warning(f"Could not register agent in portal: {e}")
    
    agent_service = MaintenanceSchedulerAgent(foundry_project_endpoint, deployment_name, cosmos_service)
    
    # Get work order
    print("1. Retrieving work order...")
    work_order_id = sys.argv[1] if len(sys.argv) > 1 else "wo-2024-468"
    
    try:
        work_order = await cosmos_service.get_work_order(work_order_id)
        print(f"   ✓ Work Order: {work_order.id}")
        print(f"   Machine: {work_order.machine_id}")
        print(f"   Fault: {work_order.fault_type}")
        print(f"   Priority: {work_order.priority}\n")
    except Exception as e:
        print(f"   ✗ Error: {str(e)}")
        return
    
    # Get historical data
    print("2. Analyzing historical maintenance data...")
    history = await cosmos_service.get_maintenance_history(work_order.machine_id)
    print(f"   ✓ Found {len(history)} historical maintenance records\n")
    
    # Get maintenance windows
    print("3. Checking available maintenance windows...")
    windows = await cosmos_service.get_available_maintenance_windows(14)
    print(f"   ✓ Found {len(windows)} available windows in next 14 days\n")
    
    # Run AI analysis
    print("4. Running AI predictive analysis...")
    try:
        schedule = await agent_service.predict_schedule(work_order, history, windows)
        print(f"   ✓ Analysis complete!\n")
        
        # Display results
        print("=== Predictive Maintenance Schedule ===")
        print(f"Schedule ID: {schedule.id}")
        print(f"Machine: {schedule.machine_id}")
        print(f"Scheduled Date: {schedule.scheduled_date.strftime('%Y-%m-%d %H:%M')}")
        print(f"Window: {schedule.maintenance_window.start_time.strftime('%H:%M')} - {schedule.maintenance_window.end_time.strftime('%H:%M')}")
        print(f"Production Impact: {schedule.maintenance_window.production_impact}")
        print(f"Risk Score: {schedule.risk_score}/100")
        print(f"Failure Probability: {schedule.predicted_failure_probability * 100:.1f}%")
        print(f"Recommended Action: {schedule.recommended_action}")
        print(f"\nReasoning:")
        print(f"{schedule.reasoning}")
        print()
        
        # Save schedule
        print("5. Saving maintenance schedule...")
        await cosmos_service.save_maintenance_schedule(schedule)
        print(f"   ✓ Schedule saved to Cosmos DB\n")
        
        # Update work order
        print("6. Updating work order status...")
        await cosmos_service.update_work_order_status(work_order.id, "Scheduled")
        print(f"   ✓ Work order status updated to 'Scheduled'\n")
        
        print("✓ Predictive Maintenance Agent completed successfully!")
    except Exception as e:
        print(f"   ✗ Error during predictive analysis: {str(e)}")
        import traceback
        print(f"\nStack trace:\n{traceback.format_exc()}")


if __name__ == "__main__":
    asyncio.run(main())
