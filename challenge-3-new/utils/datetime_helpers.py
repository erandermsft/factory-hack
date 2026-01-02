"""Datetime utility functions."""
from datetime import datetime
from typing import Optional


def parse_datetime(dt_str) -> Optional[datetime]:
    """Parse datetime from ISO string.
    
    Args:
        dt_str: ISO datetime string, datetime object, or None
        
    Returns:
        Parsed datetime object or None
    """
    if isinstance(dt_str, datetime):
        return dt_str
    if not dt_str:
        return None
    try:
        return datetime.fromisoformat(dt_str.replace('Z', '+00:00'))
    except Exception:
        return None
