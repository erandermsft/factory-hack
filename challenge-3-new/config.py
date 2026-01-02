"""Configuration management for the maintenance scheduler."""
import os
from dataclasses import dataclass
from typing import Optional
from dotenv import load_dotenv


@dataclass
class Config:
    """Application configuration from environment variables."""
    cosmos_endpoint: str
    cosmos_key: str
    cosmos_database_name: str
    foundry_project_endpoint: str
    foundry_model_deployment_name: str = "gpt-4o"
    applicationinsights_connection_string: Optional[str] = None


def load_config() -> Config:
    """Load and validate configuration from environment variables.
    
    Returns:
        Config object with all required settings
        
    Raises:
        ValueError: If required environment variables are missing
    """
    load_dotenv(override=True)
    
    cosmos_endpoint = os.getenv("COSMOS_ENDPOINT")
    cosmos_key = os.getenv("COSMOS_KEY")
    database_name = os.getenv("COSMOS_DATABASE_NAME")
    foundry_project_endpoint = os.getenv("FOUNDRY_PROJECT_ENDPOINT")
    
    # Validate required variables
    if not all([cosmos_endpoint, cosmos_key, database_name, foundry_project_endpoint]):
        missing = []
        if not cosmos_endpoint:
            missing.append("COSMOS_ENDPOINT")
        if not cosmos_key:
            missing.append("COSMOS_KEY")
        if not database_name:
            missing.append("COSMOS_DATABASE_NAME")
        if not foundry_project_endpoint:
            missing.append("FOUNDRY_PROJECT_ENDPOINT")
        
        raise ValueError(
            f"Missing required environment variables: {', '.join(missing)}\n"
            "Please ensure these are set in your .env file or environment."
        )
    
    return Config(
        cosmos_endpoint=cosmos_endpoint,
        cosmos_key=cosmos_key,
        cosmos_database_name=database_name,
        foundry_project_endpoint=foundry_project_endpoint,
        foundry_model_deployment_name=os.getenv("FOUNDRY_MODEL_DEPLOYMENT_NAME", "gpt-4o"),
        applicationinsights_connection_string=os.getenv("APPLICATIONINSIGHTS_CONNECTION_STRING"),
    )
