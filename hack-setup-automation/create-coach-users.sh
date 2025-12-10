#!/bin/bash
#
# This script creates coach users for the Factory Operations Hack
# Coach users have elevated permissions to manage the hack environment
#
# Usage: ./create-coach-users.sh [--coach-prefix COACH_PREFIX] [--count COUNT] [--domain DOMAIN]

set -e

# Default values
ADMIN_PREFIX="coach"
ADMIN_COUNT=1
DOMAIN=""
PASSWORD_LENGTH=16

# Parse command line arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --coach-prefix) COACH_PREFIX="$2"; shift ;;
        --count) COACH_COUNT="$2"; shift ;;
        --domain) DOMAIN="$2"; shift ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --coach-prefix PREFIX   Prefix for coach usernames (default: factorycoach)"
            echo "  --count COUNT           Number of coach users to create (default: 1)"
            echo "  --domain DOMAIN         Custom domain for the tenant (optional)"
            echo "  --help                  Show this help message"
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
echo "Factory Operations Hack - Coach User Creation"
echo "=========================================="
echo "Tenant: $TENANT_NAME"
echo "Tenant ID: $TENANT_ID"
echo "Domain: $DOMAIN"
echo "Coach Prefix: $COACH_PREFIX"
echo "Number of Coaches: $COACH_COUNT"
echo "=========================================="
echo ""

# Generate random password
generate_password() {
    openssl rand -base64 $PASSWORD_LENGTH | tr -d "=+/" | cut -c1-$PASSWORD_LENGTH
}

# Create output file for credentials
CREDS_FILE="coach-credentials.txt"
echo "Factory Operations Hack - Coach Credentials" > $CREDS_FILE
echo "Generated on: $(date)" >> $CREDS_FILE
echo "Tenant: $TENANT_NAME ($TENANT_ID)" >> $CREDS_FILE
echo "Domain: $DOMAIN" >> $CREDS_FILE
echo "========================================" >> $CREDS_FILE
echo "" >> $CREDS_FILE

# Create coach users
for i in $(seq 1 $COACH_COUNT); do
    if [ $COACH_COUNT -eq 1 ]; then
        USERNAME="${COACH_PREFIX}"
    else
        USERNAME="${COACH_PREFIX}${i}"
    fi
    
    UPN="${USERNAME}@${DOMAIN}"
    DISPLAY_NAME="Factory Coach ${i}"
    PASSWORD=$(generate_password)
    
    echo "👤 Creating admin user: $USERNAME..."
    
    # Check if user already exists
    EXISTING_USER=$(az ad user list --filter "userPrincipalName eq '$UPN'" --query "[].id" -o tsv 2>/dev/null || echo "")
    
    if [ -n "$EXISTING_USER" ]; then
        echo "⚠️  User $UPN already exists. Skipping..."
        echo "User: $UPN - ALREADY EXISTS (not modified)" >> $CREDS_FILE
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
    
    # Assign Owner role at subscription level
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    
    echo "   Assigning Owner role..."
    az role assignment create \
        --assignee "$USER_ID" \
        --role "Owner" \
        --scope "/subscriptions/$SUBSCRIPTION_ID" \
        &>/dev/null || echo "   ⚠️  Could not assign Owner role"
    
    # Save credentials
    echo "Username: $UPN" >> $CREDS_FILE
    echo "Password: $PASSWORD" >> $CREDS_FILE
    echo "Role: Owner" >> $CREDS_FILE
    echo "" >> $CREDS_FILE
    
    echo "✅ Coach user $USERNAME created successfully"
    echo ""
done

echo "=========================================="
echo "✅ Coach user creation completed!"
echo "=========================================="
echo ""
echo "📄 Credentials saved to: $CREDS_FILE"
echo ""
echo "⚠️  IMPORTANT: "
echo "   - Store the credentials file securely"
echo "   - Share credentials with coaches through a secure channel"
echo "   - Consider enabling MFA for coach accounts"
echo ""
