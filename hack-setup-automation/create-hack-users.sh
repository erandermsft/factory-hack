#!/bin/bash
#
# This script creates participant/hack users for the Factory Operations Hack
# Each user will get limited permissions appropriate for hack participants
# Note: Resource groups will be created per user in the deploy-infrastructure.sh script
#
# Usage: ./create-hack-users.sh [--user-prefix USER_PREFIX] [--count COUNT] [--domain DOMAIN]

set -e

# Default values
USER_PREFIX="user"
USER_COUNT=5
DOMAIN=""
PASSWORD_LENGTH=16

# Parse command line arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --user-prefix) USER_PREFIX="$2"; shift ;;
        --count) USER_COUNT="$2"; shift ;;
        --domain) DOMAIN="$2"; shift ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --user-prefix PREFIX    Prefix for hack usernames (default: factoryuser)"
            echo "  --count COUNT           Number of users to create (default: 5)"
            echo "  --domain DOMAIN         Custom domain for the tenant (optional)"
            echo "  --help                  Show this help message"
            echo ""
            echo "Note: Each user will get their own resource group deployed in the next step"
            exit 0
            ;;
        *) echo "Unknown parameter: $1"; exit 1 ;;
    esac
    shift
done

# Check if user is logged in to Azure
echo "🔍 Checking Azure login status..."
if ! az account show &>/dev/null; then
    echo "❌ Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

# Get current tenant information
TENANT_ID=$(az account show --query tenantId -o tsv)
TENANT_NAME=$(az account show --query name -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

if [ -z "$DOMAIN" ]; then
    # Try to get the default domain
    DOMAIN=$(az rest --method get --url "https://graph.microsoft.com/v1.0/domains" --query "value[?isDefault].id" -o tsv 2>/dev/null || echo "")
    
    if [ -z "$DOMAIN" ]; then
        echo "⚠️  Could not automatically detect domain. Please provide one:"
        read -p "Enter your Azure AD domain (e.g., contoso.onmicrosoft.com): " DOMAIN
    fi
fi

echo ""
echo "=========================================="
echo "Factory Operations Hack - User Creation"
echo "=========================================="
echo "Tenant: $TENANT_NAME"
echo "Tenant ID: $TENANT_ID"
echo "Domain: $DOMAIN"
echo "User Prefix: $USER_PREFIX"
echo "Number of Users: $USER_COUNT"
echo "Note: Each user will get their own resource group"
echo "=========================================="
echo ""

# Generate random password
generate_password() {
    openssl rand -base64 $PASSWORD_LENGTH | tr -d "=+/" | cut -c1-$PASSWORD_LENGTH
}

# Create output file for credentials
CREDS_FILE="hack-user-credentials.txt"
echo "Factory Operations Hack - User Credentials" > $CREDS_FILE
echo "Generated on: $(date)" >> $CREDS_FILE
echo "Tenant: $TENANT_NAME ($TENANT_ID)" >> $CREDS_FILE
echo "Domain: $DOMAIN" >> $CREDS_FILE
echo "Note: Each user will get their own resource group deployed" >> $CREDS_FILE
echo "========================================" >> $CREDS_FILE
echo "" >> $CREDS_FILE

# Create CSV file for easy distribution
CSV_FILE="hack-user-credentials.csv"
echo "Username,Password,Email,ResourceGroup" > $CSV_FILE

# Create hack users
for i in $(seq 1 $USER_COUNT); do
    if [ $USER_COUNT -eq 1 ]; then
        USERNAME="${USER_PREFIX}"
    else
        USERNAME="${USER_PREFIX}${i}"
    fi
    
    UPN="${USERNAME}@${DOMAIN}"
    DISPLAY_NAME="Factory Hack User ${i}"
    PASSWORD=$(generate_password)
    
    echo "👤 Creating hack user: $USERNAME..."
    
    # Check if user already exists
    EXISTING_USER=$(az ad user list --filter "userPrincipalName eq '$UPN'" --query "[].id" -o tsv 2>/dev/null || echo "")
    
    if [ -n "$EXISTING_USER" ]; then
        echo "⚠️  User $UPN already exists. Fetching details..."
        USER_ID="$EXISTING_USER"
        
        # Define the resource group name for this user
        TEAM_RG="${USERNAME}-rg"
        
        # Save credentials to text file (password unknown for existing users)
        echo "Team ${i}:" >> $CREDS_FILE
        echo "Username: $UPN" >> $CREDS_FILE
        echo "Password: [EXISTING USER - Password Unknown]" >> $CREDS_FILE
        echo "User ID: $USER_ID" >> $CREDS_FILE
        echo "Resource Group: $TEAM_RG (to be created)" >> $CREDS_FILE
        echo "" >> $CREDS_FILE
        
        # Save to CSV with placeholder password
        echo "$UPN,[EXISTING],$USER_ID,$TEAM_RG" >> $CSV_FILE
        
        echo "✅ Existing user $USERNAME recorded"
        echo ""
        continue
    fi
    
    # Create user
    USER_ID=$(az ad user create \
        --display-name "$DISPLAY_NAME" \
        --user-principal-name "$UPN" \
        --password "$PASSWORD" \
        --force-change-password-next-sign-in false \
        --query id -o tsv 2>/dev/null)
    
    if [ -z "$USER_ID" ]; then
        echo "❌ Failed to create user $UPN"
        echo "User: $UPN - FAILED TO CREATE" >> $CREDS_FILE
        continue
    fi
    
    echo "✅ Created user: $UPN (ID: $USER_ID)"
    
    # Define the resource group name for this user
    TEAM_RG="${USERNAME}-rg"
    
    # Store user info for infrastructure deployment
    echo "   → Resource Group: $TEAM_RG (to be created)"
    
    # Save credentials to text file
    echo "Team ${i}:" >> $CREDS_FILE
    echo "Username: $UPN" >> $CREDS_FILE
    echo "Password: $PASSWORD" >> $CREDS_FILE
    echo "User ID: $USER_ID" >> $CREDS_FILE
    echo "Resource Group: $TEAM_RG (to be created)" >> $CREDS_FILE
    echo "" >> $CREDS_FILE
    
    # Save to CSV
    echo "$UPN,$PASSWORD,$USER_ID,$TEAM_RG" >> $CSV_FILE
    
    echo "✅ Hack user $USERNAME created successfully"
    
    # Add delay to avoid Azure throttling when creating many users
    if [ $i -lt $USER_COUNT ]; then
        echo "⏱️  Waiting 3 seconds to avoid throttling..."
        sleep 3
    fi
    echo ""
done

echo "=========================================="
echo "✅ Hack user creation completed!"
echo "=========================================="
echo ""
echo "📄 Credentials saved to:"
echo "   - Text format: $CREDS_FILE"
echo "   - CSV format: $CSV_FILE"
echo ""
echo "📋 Created Users:"
for i in $(seq 1 $USER_COUNT); do
    if [ $USER_COUNT -eq 1 ]; then
        USERNAME="${USER_PREFIX}"
    else
        USERNAME="${USER_PREFIX}${i}"
    fi
    TEAM_RG="${USERNAME}-rg"
    echo "   - ${USERNAME}@${DOMAIN} → Resource Group: $TEAM_RG"
done
echo ""
echo "🎯 Next Step:"
echo "   Deploy infrastructure for each user:"
echo "   ./deploy-infrastructure.sh --csv-file $CSV_FILE"
echo ""
echo "⚠️  IMPORTANT: "
echo "   - Store the credentials files securely"
echo "   - Each user will get their own isolated resource group"
echo "   - Infrastructure deployment will take ~15-30 minutes per user"
echo ""
