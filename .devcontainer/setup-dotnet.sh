#!/bin/bash
set -e

echo "🔧 Setting up .NET environment for Challenge 2 and 3..."

# Create a temporary project to restore packages
TEMP_DIR="/tmp/dotnet-setup"
mkdir -p $TEMP_DIR
cd $TEMP_DIR

echo "📦 Creating temporary .NET project to cache NuGet packages..."
dotnet new console -n TempSetup --force

echo "📥 Installing required NuGet packages..."

# Azure SDKs
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Azure.AI.Inference --version 1.0.0-beta.5 --prerelease
dotnet add package Azure.AI.Projects --version 1.1.0
dotnet add package Azure.Identity

# Configuration
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add package Microsoft.Extensions.Configuration.Json

# Logging
dotnet add package Microsoft.Extensions.Logging.Console

# JSON handling
dotnet add package System.Text.Json
dotnet add package Newtonsoft.Json

echo "🔄 Restoring packages to cache..."
dotnet restore

echo "🧹 Cleaning up temporary project..."
cd /
rm -rf $TEMP_DIR

echo "✅ .NET environment setup complete!"
echo "📌 .NET SDK $(dotnet --version) is ready"
echo "📦 All required NuGet packages are cached and ready to use"
