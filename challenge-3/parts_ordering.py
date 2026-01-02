#!/usr/bin/env python3
"""
Parts Ordering Agent - Automated parts ordering using Microsoft Agent Framework.

This agent monitors inventory levels, evaluates supplier performance, and automates 
parts ordering to ensure required components are available when needed. It optimizes 
order timing, supplier selection, and delivery schedules to support maintenance operations.

Usage:
    python parts_ordering.py [WORK_ORDER_ID]
    
Example:
    python parts_ordering.py WO-001
"""
import os
import sys
import json
import asyncio
import uuid
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

logger = logging.getLogger(__name__)


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
class InventoryItem:
    """Inventory item from WMS"""
    id: str = ""
    part_number: str = ""
    part_name: str = ""
    current_stock: int = 0
    min_stock: int = 0
    reorder_point: int = 0
    location: str = ""


@dataclass
class Supplier:
    """Supplier information from SCM"""
    id: str = ""
    name: str = ""
    parts: List[str] = field(default_factory=list)
    lead_time_days: int = 0
    reliability: str = ""
    contact_email: str = ""


@dataclass
class OrderItem:
    """Individual item in a parts order"""
    part_number: str = ""
    part_name: str = ""
    quantity: int = 0
    unit_cost: float = 0.0
    total_cost: float = 0.0


@dataclass
class PartsOrder:
    """Parts order for SCM system"""
    id: str = ""
    work_order_id: str = ""
    order_items: List[OrderItem] = field(default_factory=list)
    supplier_id: str = ""
    supplier_name: str = ""
    total_cost: float = 0.0
    expected_delivery_date: Optional[datetime] = None
    order_status: str = "Pending"
    created_at: Optional[datetime] = None


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
    
    async def get_inventory_items(self, part_numbers: List[str]) -> List[InventoryItem]:
        """Get inventory items from WMS"""
        try:
            container = self.database.get_container_client("PartsInventory")
            results = []
            
            for part_number in part_numbers:
                query = "SELECT * FROM c WHERE c.partNumber = @partNumber OR c.id = @partNumber"
                items = list(container.query_items(
                    query=query,
                    parameters=[{"name": "@partNumber", "value": part_number}],
                    enable_cross_partition_query=True
                ))
                
                for item in items:
                    results.append(InventoryItem(
                        id=item.get('id', ''),
                        part_number=item.get('partNumber', ''),
                        part_name=item.get('partName', ''),
                        current_stock=item.get('currentStock', 0),
                        min_stock=item.get('minStock', 0),
                        reorder_point=item.get('reorderPoint', 0),
                        location=item.get('location', '')
                    ))
            
            return results
        except Exception as e:
            print(f"Warning: Could not retrieve inventory: {str(e)}")
            return []
    
    async def get_suppliers_for_parts(self, part_numbers: List[str]) -> List[Supplier]:
        """Get suppliers from SCM that can provide specific parts"""
        try:
            container = self.database.get_container_client("Suppliers")
            query = "SELECT * FROM c"
            items = list(container.query_items(query=query, enable_cross_partition_query=True))
            
            results = []
            for item in items:
                supplier_parts = item.get('parts', [])
                if any(part in supplier_parts for part in part_numbers):
                    results.append(Supplier(
                        id=item.get('id', ''),
                        name=item.get('name', ''),
                        parts=supplier_parts,
                        lead_time_days=item.get('leadTimeDays', 0),
                        reliability=item.get('reliability', ''),
                        contact_email=item.get('contactEmail', '')
                    ))
            
            return results if results else self._generate_mock_suppliers()
        except Exception as e:
            print(f"Warning: Could not retrieve suppliers: {str(e)}")
            return self._generate_mock_suppliers()
    
    def _generate_mock_suppliers(self) -> List[Supplier]:
        """Generate mock suppliers"""
        return [
            Supplier(
                id="supplier-001",
                name="Industrial Parts Supply Co.",
                parts=[],
                reliability="High",
                lead_time_days=3,
                contact_email="orders@industrialparts.com"
            ),
            Supplier(
                id="supplier-002",
                name="Quick Parts Ltd.",
                parts=[],
                reliability="Medium",
                lead_time_days=1,
                contact_email="sales@quickparts.com"
            )
        ]
    
    async def save_parts_order(self, order: PartsOrder) -> PartsOrder:
        """Save parts order to SCM"""
        try:
            container = self.database.get_container_client("PartsOrders")
        except:
            self.database.create_container(id="PartsOrders", partition_key=PartitionKey(path="/id"))
            container = self.database.get_container_client("PartsOrders")
        
        item = {
            "id": order.id,
            "workOrderId": order.work_order_id,
            "orderItems": [
                {
                    "partNumber": oi.part_number,
                    "partName": oi.part_name,
                    "quantity": oi.quantity,
                    "unitCost": oi.unit_cost,
                    "totalCost": oi.total_cost
                } for oi in order.order_items
            ],
            "supplierId": order.supplier_id,
            "supplierName": order.supplier_name,
            "totalCost": order.total_cost,
            "expectedDeliveryDate": order.expected_delivery_date.isoformat() if order.expected_delivery_date else None,
            "orderStatus": order.order_status,
            "createdAt": order.created_at.isoformat() if order.created_at else None
        }
        
        container.create_item(body=item)
        return order
    
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
    
    async def get_work_order_chat_history(self, work_order_id: str) -> Optional[str]:
        """Get chat history for a work order"""
        try:
            container = self.database.get_container_client("ChatHistories")
            item = container.read_item(item=work_order_id, partition_key=work_order_id)
            return item.get('historyJson')
        except exceptions.CosmosResourceNotFoundError:
            return None
        except Exception:
            return None
    
    async def save_work_order_chat_history(self, work_order_id: str, history_json: str):
        """Save chat history for a work order"""
        try:
            container = self.database.get_container_client("ChatHistories")
        except:
            self.database.create_container(id="ChatHistories", partition_key=PartitionKey(path="/entityId"))
            container = self.database.get_container_client("ChatHistories")
        
        item = {
            "id": work_order_id,
            "entityId": work_order_id,
            "entityType": "workorder",
            "historyJson": history_json,
            "purpose": "parts_ordering",
            "updatedAt": datetime.utcnow().isoformat()
        }
        
        container.upsert_item(body=item)


# =============================================================================
# Agent Service
# =============================================================================

class PartsOrderingAgent:
    """AI Agent for parts ordering"""
    
    def __init__(self, project_endpoint: str, deployment_name: str, cosmos_service: CosmosDbService):
        self.project_endpoint = project_endpoint
        self.deployment_name = deployment_name
        self.cosmos_service = cosmos_service
    
    async def generate_order(
        self,
        work_order: WorkOrder,
        inventory: List[InventoryItem],
        suppliers: List[Supplier]
    ) -> PartsOrder:
        """Generate optimized parts order using AI"""
        
        context = self._build_context(work_order, inventory, suppliers)
        chat_history_json = await self.cosmos_service.get_work_order_chat_history(work_order.id)
        print(f"   Using persistent chat history for work order: {work_order.id}")
        
        instructions = """You are a parts ordering specialist for industrial tire manufacturing equipment.

Analyze inventory status and optimize parts ordering from suppliers considering:
1. Current inventory levels vs reorder points
2. Supplier reliability, lead time, and cost
3. Previous order history
4. Order urgency based on work order priority

Always respond in valid JSON format as requested."""
        
        credential = DefaultAzureCredential()
        
        async with ChatAgent(
            chat_client=AzureAIAgentClient(
                project_endpoint=self.project_endpoint,
                model_deployment_name=self.deployment_name,
                credential=credential,
                agent_name=f"PartsOrdering-{work_order.id}",
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
            
            await self._save_thread_history(work_order.id, thread)
        
        json_response = self._extract_json(response_text)
        data = json.loads(json_response)
        
        return PartsOrder(
            id=f"PO-{str(uuid.uuid4())[:8]}",
            work_order_id=work_order.id,
            order_items=[
                OrderItem(
                    part_number=item['partNumber'],
                    part_name=item['partName'],
                    quantity=item['quantity'],
                    unit_cost=item['unitCost'],
                    total_cost=item['totalCost']
                ) for item in data['orderItems']
            ],
            supplier_id=data['supplierId'],
            supplier_name=data['supplierName'],
            total_cost=data['totalCost'],
            expected_delivery_date=datetime.fromisoformat(data['expectedDeliveryDate'].replace('Z', '+00:00')),
            order_status="Pending",
            created_at=datetime.utcnow()
        )
    
    async def _save_thread_history(self, work_order_id: str, thread):
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
            await self.cosmos_service.save_work_order_chat_history(work_order_id, json.dumps(messages))
        except Exception as e:
            print(f"   Warning: Could not save chat history: {e}")
    
    def _build_context(self, work_order: WorkOrder, inventory: List[InventoryItem], suppliers: List[Supplier]) -> str:
        """Build analysis context for AI"""
        lines = [
            "# Parts Ordering Analysis Request",
            "",
            "## Work Order Information",
            f"- Work Order ID: {work_order.id}",
            f"- Machine ID: {work_order.machine_id}",
            f"- Fault Type: {work_order.fault_type}",
            f"- Priority: {work_order.priority}",
            "",
            "## Required Parts"
        ]
        
        for part in work_order.required_parts:
            lines.append(f"- **{part.part_name}** (Part#: {part.part_number})")
            lines.append(f"  * Quantity needed: {part.quantity}")
            lines.append(f"  * Available in stock: {'YES' if part.is_available else 'NO'}")
        
        lines.extend(["", "## Current Inventory Status"])
        
        if inventory:
            for item in inventory:
                needs_order = item.current_stock <= item.reorder_point
                lines.append(f"- **{item.part_name}** (Part#: {item.part_number})")
                lines.append(f"  * Current Stock: {item.current_stock}")
                lines.append(f"  * Minimum Stock: {item.min_stock}")
                lines.append(f"  * Reorder Point: {item.reorder_point}")
                lines.append(f"  * Status: {'⚠️  NEEDS ORDERING' if needs_order else '✓ Adequate'}")
                lines.append(f"  * Location: {item.location}")
        else:
            lines.append("⚠️  No inventory records found for required parts.")
        
        lines.extend(["", "## Available Suppliers"])
        
        if suppliers:
            for supplier in suppliers:
                lines.append(f"- **{supplier.name}** (ID: {supplier.id})")
                lines.append(f"  * Lead Time: {supplier.lead_time_days} days")
                lines.append(f"  * Reliability: {supplier.reliability}")
                lines.append(f"  * Contact: {supplier.contact_email}")
                parts_preview = ", ".join(supplier.parts[:5])
                if len(supplier.parts) > 5:
                    parts_preview += "..."
                lines.append(f"  * Parts Available: {parts_preview}")
        else:
            lines.append("⚠️  No suppliers found for required parts!")
        
        lines.extend([
            "",
            "## Analysis Required",
            "Please provide a JSON response with:",
            "1. Parts to order",
            "2. Optimal supplier selection (reliability > lead time > cost)",
            "3. Expected delivery date",
            "4. Total order cost",
            "",
            "```json",
            "{",
            '  "supplierId": "<supplier ID>",',
            '  "supplierName": "<supplier name>",',
            '  "orderItems": [',
            '    {',
            '      "partNumber": "<part number>",',
            '      "partName": "<part name>",',
            '      "quantity": <number>,',
            '      "unitCost": <decimal>,',
            '      "totalCost": <decimal>',
            '    }',
            '  ],',
            '  "totalCost": <decimal>,',
            '  "expectedDeliveryDate": "<ISO datetime>",',
            '  "reasoning": "<explanation>"',
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
    print("=== Parts Ordering Agent ===\n")
    
    # Load configuration
    cosmos_endpoint = os.getenv("COSMOS_ENDPOINT")
    cosmos_key = os.getenv("COSMOS_KEY")
    database_name = os.getenv("COSMOS_DATABASE_NAME")
    foundry_project_endpoint = os.getenv("FOUNDRY_PROJECT_ENDPOINT")
    deployment_name = os.getenv("FOUNDRY_MODEL_DEPLOYMENT_NAME", "gpt-4o")
    
    # Validate
    if not all([cosmos_endpoint, cosmos_key, database_name, foundry_project_endpoint]):
        print("Error: Missing required environment variables.")
        print("Required: COSMOS_ENDPOINT, COSMOS_KEY, COSMOS_DATABASE_NAME, FOUNDRY_PROJECT_ENDPOINT")
        return
    
    # Initialize
    cosmos_service = CosmosDbService(cosmos_endpoint, cosmos_key, database_name)
    
    # Register agent in Azure AI Foundry portal
    async with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=foundry_project_endpoint, credential=credential) as project_client,
    ):
        try:
            from azure.ai.projects.models import PromptAgentDefinition
            
            # Get current agent version
            current_version = 0
            try:
                async for agent in project_client.agents.list():
                    if agent.name == "PartsOrderingAgent" and agent.metadata:
                        current_version = max(current_version, float(agent.metadata.get("version", "0")))
            except:
                pass
            
            new_version = current_version + 1
            
            # Create agent definition
            definition = PromptAgentDefinition(
                model=deployment_name,
                instructions="""You are a Parts Ordering Specialist for industrial tire manufacturing equipment.

Analyze inventory levels and optimize parts ordering from suppliers considering:
1. Current inventory levels vs reorder points
2. Supplier reliability, lead time, and cost
3. Previous order history  
4. Order urgency based on work order priority

When generating orders:
- Prioritize suppliers with high reliability
- Balance lead time against urgency
- Consider total cost optimization
- Reference inventory data to determine quantities

Always respond in valid JSON format with: supplierId, supplierName, orderItems (partNumber, partName, quantity, unitCost, totalCost), totalCost, expectedDeliveryDate, and reasoning.""",
            )
            
            # Create new version
            print(f"   Registering PartsOrderingAgent version {new_version} in portal...")
            logger.info(f"Creating PartsOrderingAgent version {new_version}")
            registered_agent = await project_client.agents.create_version(
                agent_name="PartsOrderingAgent",
                definition=definition,
                description=f"Parts ordering automation agent v{new_version}",
                metadata={
                    "version": str(new_version),
                    "framework": "agent-framework",
                    "purpose": "parts_ordering"
                }
            )
            print(f"   ✓ PartsOrderingAgent v{new_version} registered in portal!")
            logger.info(f"PartsOrderingAgent v{new_version} registered in portal")
        except Exception as e:
            print(f"   ✗ Could not register agent in portal: {e}")
            logger.warning(f"Could not register agent in portal: {e}")
    
    agent_service = PartsOrderingAgent(foundry_project_endpoint, deployment_name, cosmos_service)
    
    # Get work order
    print("1. Retrieving work order...")
    work_order_id = sys.argv[1] if len(sys.argv) > 1 else "WO-001"
    
    try:
        work_order = await cosmos_service.get_work_order(work_order_id)
        print(f"   ✓ Work Order: {work_order.id}")
        print(f"   Machine: {work_order.machine_id}")
        print(f"   Required Parts: {len(work_order.required_parts)}")
        print(f"   Priority: {work_order.priority}\n")
    except Exception as e:
        print(f"   ✗ Error: {str(e)}")
        return
    
    # Check inventory
    print("2. Checking inventory status...")
    part_numbers = [p.part_number for p in work_order.required_parts]
    inventory = await cosmos_service.get_inventory_items(part_numbers)
    print(f"   ✓ Found {len(inventory)} inventory records\n")
    
    # Identify parts needing order
    parts_needing_order = [p for p in work_order.required_parts if not p.is_available]
    
    if not parts_needing_order:
        print("✓ All required parts are available in stock!")
        print("No parts order needed.\n")
        
        print("3. Updating work order status...")
        await cosmos_service.update_work_order_status(work_order.id, "Ready")
        print(f"   ✓ Work order status updated to 'Ready'\n")
        
        print("✓ Parts Ordering Agent completed successfully!")
        return
    
    print(f"⚠️  {len(parts_needing_order)} part(s) need to be ordered:")
    for part in parts_needing_order:
        print(f"   - {part.part_name} (Qty: {part.quantity})")
    print()
    
    # Get suppliers
    print("3. Finding suppliers...")
    needed_part_numbers = [p.part_number for p in parts_needing_order]
    suppliers = await cosmos_service.get_suppliers_for_parts(needed_part_numbers)
    print(f"   ✓ Found {len(suppliers)} potential suppliers\n")
    
    if not suppliers:
        print("✗ No suppliers found for required parts!")
        return
    
    # Generate order
    print("4. Running AI parts ordering analysis...")
    try:
        order = await agent_service.generate_order(work_order, inventory, suppliers)
        print(f"   ✓ Parts order generated!\n")
        
        # Display results
        print("=== Parts Order ===")
        print(f"Order ID: {order.id}")
        print(f"Work Order: {order.work_order_id}")
        print(f"Supplier: {order.supplier_name} (ID: {order.supplier_id})")
        print(f"Expected Delivery: {order.expected_delivery_date.strftime('%Y-%m-%d')}")
        print(f"Total Cost: ${order.total_cost:.2f}")
        print(f"Status: {order.order_status}")
        print(f"\nOrder Items:")
        for item in order.order_items:
            print(f"  - {item.part_name} (#{item.part_number})")
            print(f"    Qty: {item.quantity} @ ${item.unit_cost:.2f} = ${item.total_cost:.2f}")
        print()
        
        # Save order
        print("5. Saving parts order...")
        await cosmos_service.save_parts_order(order)
        print(f"   ✓ Order saved to SCM system\n")
        
        # Update work order
        print("6. Updating work order status...")
        await cosmos_service.update_work_order_status(work_order.id, "PartsOrdered")
        print(f"   ✓ Work order status updated to 'PartsOrdered'\n")
        
        print("✓ Parts Ordering Agent completed successfully!")
    except Exception as e:
        print(f"   ✗ Error during parts ordering: {str(e)}")
        import traceback
        print(f"\nStack trace:\n{traceback.format_exc()}")


if __name__ == "__main__":
    asyncio.run(main())
