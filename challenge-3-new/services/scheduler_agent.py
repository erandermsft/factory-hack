"""Maintenance scheduler AI agent service."""
import json
from datetime import datetime
from typing import List

from azure.identity.aio import DefaultAzureCredential
from agent_framework import ChatAgent
from agent_framework_azure_ai import AzureAIAgentClient

from models import WorkOrder, MaintenanceWindow, MaintenanceHistory, MaintenanceSchedule
from services.cosmos_db_service import CosmosDbService
from utils import extract_json


class MaintenanceSchedulerAgent:
    """AI Agent for predictive maintenance scheduling"""
    
    def __init__(self, project_endpoint: str, deployment_name: str, cosmos_service: CosmosDbService):
        """Initialize the maintenance scheduler agent.
        
        Args:
            project_endpoint: Azure AI Foundry project endpoint
            deployment_name: Model deployment name
            cosmos_service: CosmosDB service instance
        """
        self.project_endpoint = project_endpoint
        self.deployment_name = deployment_name
        self.cosmos_service = cosmos_service
    
    async def predict_schedule(
        self,
        work_order: WorkOrder,
        history: List[MaintenanceHistory],
        windows: List[MaintenanceWindow]
    ) -> MaintenanceSchedule:
        """Predict optimal maintenance schedule using AI.
        
        Args:
            work_order: Work order to schedule
            history: Historical maintenance records
            windows: Available maintenance windows
            
        Returns:
            MaintenanceSchedule with AI predictions
        """
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
            
            # Restore chat history if available
            if chat_history_json:
                try:
                    for msg in json.loads(chat_history_json):
                        await thread.add_message(role=msg["role"], content=msg["content"])
                except Exception as e:
                    print(f"   Warning: Could not restore chat history: {e}")
            
            # Run AI analysis
            result = await agent.run(context, thread=thread)
            response_text = result.text
            
            # Save chat history
            await self._save_thread_history(work_order.machine_id, thread)
        
        # Parse response
        json_response = extract_json(response_text)
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
        """Save thread history to Cosmos DB.
        
        Args:
            machine_id: Machine ID for the chat history
            thread: Agent thread to save
        """
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
    
    def _build_context(
        self,
        work_order: WorkOrder,
        history: List[MaintenanceHistory],
        windows: List[MaintenanceWindow]
    ) -> str:
        """Build analysis context for AI.
        
        Args:
            work_order: Work order details
            history: Historical maintenance records
            windows: Available maintenance windows
            
        Returns:
            Formatted context string for the AI agent
        """
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
