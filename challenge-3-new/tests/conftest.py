"""Test configuration and shared fixtures."""
import pytest
from unittest.mock import AsyncMock, Mock


@pytest.fixture
def mock_cosmos_client():
    """Mock Cosmos DB client for testing."""
    mock_client = Mock()
    mock_database = Mock()
    mock_container = Mock()
    
    mock_client.get_database_client.return_value = mock_database
    mock_database.get_container_client.return_value = mock_container
    
    return mock_client


@pytest.fixture
def sample_config():
    """Sample configuration for testing."""
    from config import Config
    
    return Config(
        cosmos_endpoint="https://test.documents.azure.com:443/",
        cosmos_key="test_key",
        cosmos_database_name="test_db",
        foundry_project_endpoint="https://test.openai.azure.com/",
        foundry_model_deployment_name="gpt-4o",
        applicationinsights_connection_string=None
    )
