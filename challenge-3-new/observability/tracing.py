"""OpenTelemetry tracing setup for Azure AI monitoring."""
from typing import Optional

# Check for tracing availability
try:
    from agent_framework.observability import configure_otel_providers
    from azure.monitor.opentelemetry.exporter import (
        AzureMonitorTraceExporter,
        AzureMonitorMetricExporter,
        AzureMonitorLogExporter
    )
    TRACING_AVAILABLE = True
except ImportError:
    TRACING_AVAILABLE = False


def setup_tracing(connection_string: Optional[str] = None) -> bool:
    """Setup Azure AI tracing with OpenTelemetry.
    
    Args:
        connection_string: Application Insights connection string
        
    Returns:
        True if tracing was successfully enabled, False otherwise
    """
    if not TRACING_AVAILABLE:
        print("⚠️  Agent Framework observability not available.")
        return False
    
    if not connection_string:
        print("⚠️  Tracing available but APPLICATIONINSIGHTS_CONNECTION_STRING not set")
        return False
    
    try:
        # Configure OpenTelemetry with Azure Monitor exporters
        trace_exporter = AzureMonitorTraceExporter.from_connection_string(connection_string)
        metric_exporter = AzureMonitorMetricExporter.from_connection_string(connection_string)
        log_exporter = AzureMonitorLogExporter.from_connection_string(connection_string)
        
        configure_otel_providers(
            enable_sensitive_data=True,  # Capture prompts and completions
            exporters=[trace_exporter, metric_exporter, log_exporter]
        )
        
        print("📊 Agent Framework tracing enabled (Azure Monitor)")
        print(f"   Traces sent to: {connection_string.split(';')[0]}")
        print("   View in Azure AI Foundry portal: https://ai.azure.com -> Your Project -> Tracing\n")
        return True
    except Exception as e:
        print(f"⚠️  Tracing setup failed: {e}\n")
        return False
