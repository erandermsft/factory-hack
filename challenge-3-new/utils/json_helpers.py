"""JSON utility functions."""


def extract_json(response: str) -> str:
    """Extract JSON from agent response.
    
    Handles both markdown code blocks and plain JSON.
    
    Args:
        response: Agent response text that may contain JSON
        
    Returns:
        Extracted JSON string
        
    Raises:
        Exception: If no valid JSON found in response
    """
    # Try to extract from markdown code block
    if "```json" in response:
        start = response.index("```json") + 7
        end = response.index("```", start)
        return response[start:end].strip()
    
    # Try to find JSON by braces
    start = response.find('{')
    if start >= 0:
        end = response.rfind('}')
        return response[start:end+1]
    
    raise Exception("Could not extract JSON from agent response")
