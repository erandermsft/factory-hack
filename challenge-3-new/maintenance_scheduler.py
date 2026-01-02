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
import sys
import asyncio

from config import load_config
from models import MaintenanceSchedule
from services import CosmosDbService, MaintenanceSchedulerAgent
from observability import setup_tracing, register_agent


async def main():
    """Main program"""
    print("=== Predictive Maintenance Agent ===\n")
    
    # Load configuration
    try:
        config = load_config()
    except ValueError as e:
        print(f"Error: {e}")
        return
    
    # Enable Azure AI Tracing
    setup_tracing(config.applicationinsights_connection_string)
    
    # Initialize services
    cosmos_service = CosmosDbService(
        config.cosmos_endpoint,
        config.cosmos_key,
        config.cosmos_database_name
    )
    
    # Register agent in Azure AI Foundry portal
    agent_instructions = """You are a Predictive Maintenance Scheduler for industrial tire manufacturing equipment.

Analyze work orders, historical maintenance data, and available maintenance windows to:
1. Assess equipment failure risk based on historical patterns and work order priority
2. Identify optimal maintenance windows that minimize production disruption
3. Generate predictive maintenance schedules with risk scores and recommendations

Consider factors like:
- Work order priority (high/medium/low)
- Historical maintenance frequency and patterns
- Production impact of maintenance windows
- Equipment estimated repair duration

Output JSON with: scheduled_date, risk_score (0-100), predicted_failure_probability (0-1), recommended_action (IMMEDIATE/URGENT/SCHEDULED/MONITOR), and reasoning."""
    
    await register_agent(
        project_endpoint=config.foundry_project_endpoint,
        agent_name="MaintenanceSchedulerAgent",
        deployment_name=config.foundry_model_deployment_name,
        instructions=agent_instructions,
        description="Predictive maintenance scheduling agent for industrial equipment"
    )
    
    agent_service = MaintenanceSchedulerAgent(
        config.foundry_project_endpoint,
        config.foundry_model_deployment_name,
        cosmos_service
    )
    
    # Get work order ID from CLI args or use default
    work_order_id = sys.argv[1] if len(sys.argv) > 1 else "wo-2024-468"
    
    # Execute workflow
    try:
        # Step 1: Retrieve work order
        print("1. Retrieving work order...")
        work_order = await cosmos_service.get_work_order(work_order_id)
        print(f"   ✓ Work Order: {work_order.id}")
        print(f"   Machine: {work_order.machine_id}")
        print(f"   Fault: {work_order.fault_type}")
        print(f"   Priority: {work_order.priority}\n")
        
        # Step 2: Get historical data
        print("2. Analyzing historical maintenance data...")
        history = await cosmos_service.get_maintenance_history(work_order.machine_id)
        print(f"   ✓ Found {len(history)} historical maintenance records\n")
        
        # Step 3: Get maintenance windows
        print("3. Checking available maintenance windows...")
        windows = await cosmos_service.get_available_maintenance_windows(14)
        print(f"   ✓ Found {len(windows)} available windows in next 14 days\n")
        
        # Step 4: Run AI analysis
        print("4. Running AI predictive analysis...")
        schedule = await agent_service.predict_schedule(work_order, history, windows)
        print(f"   ✓ Analysis complete!\n")
        
        # Step 5: Display results
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
        
        # Step 6: Save schedule
        print("5. Saving maintenance schedule...")
        await cosmos_service.save_maintenance_schedule(schedule)
        print(f"   ✓ Schedule saved to Cosmos DB\n")
        
        # Step 7: Update work order status
        print("6. Updating work order status...")
        await cosmos_service.update_work_order_status(work_order.id, "Scheduled")
        print(f"   ✓ Work order status updated to 'Scheduled'\n")
        
        print("✓ Predictive Maintenance Agent completed successfully!")
        
    except Exception as e:
        print(f"   ✗ Error: {str(e)}")
        import traceback
        print(f"\nStack trace:\n{traceback.format_exc()}")
        return


if __name__ == "__main__":
    asyncio.run(main())
