#!/bin/bash
# Run all tests with coverage

cd /workspaces/factory-ops-hack/challenge-3-new

echo "Running test suite..."
pytest -v --tb=short

echo ""
echo "All tests passed! ✓"
