#!/usr/bin/env bash
set -e

# Load environment variables from .env in parent directory (AZURE_SUBSCRIPTION_ID, RESOURCE_GROUP, APIM_NAME)
if [ -f ../.env ]; then
    set -a
    source ../.env
    set +a
    echo "✅ Loaded environment variables from ../.env"
else
    echo "⚠️ .env file not found. Make sure AZURE_SUBSCRIPTION_ID, RESOURCE_GROUP, and APIM_NAME are set in the environment."
fi

# Validate required env vars
: "${AZURE_SUBSCRIPTION_ID:?Missing AZURE_SUBSCRIPTION_ID}"
: "${RESOURCE_GROUP:?Missing RESOURCE_GROUP}"
: "${APIM_NAME:?Missing APIM_NAME}"

echo "📦 Installing Azure SDKs for APIM..."
pip3 install azure-identity azure-mgmt-apimanagement==4.0.0 --quiet

echo "📝 Generating APIM mock setup script..."
cat > seed_apim_mock.py << 'EOF'
import os, json
from pathlib import Path
from azure.identity import AzureCliCredential
from azure.mgmt.apimanagement import ApiManagementClient
from azure.mgmt.apimanagement.models import (
    ApiCreateOrUpdateParameter,
    OperationContract,
    ParameterContract,
    ResponseContract,
    Protocol,
    PolicyContract
)

# Env
sub_id = os.environ.get("AZURE_SUBSCRIPTION_ID")
rg = os.environ.get("RESOURCE_GROUP")
service = os.environ.get("APIM_NAME")
api_id = "machine-api"

missing = [k for k,v in {"AZURE_SUBSCRIPTION_ID":sub_id, "RESOURCE_GROUP":rg, "APIM_NAME":service}.items() if not v]
if missing:
    raise RuntimeError(f"Missing environment variables: {', '.join(missing)}")

# Load machines JSON from repo
data_path = Path("data/machines.json").resolve()
machines = json.loads(data_path.read_text(encoding="utf-8"))
if not isinstance(machines, list):
    raise RuntimeError("machines.json must be a JSON array")

# Helper: wrap JSON in CDATA for safe XML embedding
def cdata(obj):
    return f"<![CDATA[{json.dumps(obj)}]]>"

# Policy for GET /machines (returns full JSON array)
policy_all = f"""
<policies>
  <inbound>
    <base />
    <return-response>
      <set-status code="200" reason="OK" />
      <set-header name="Content-Type" exists-action="override">
        <value>application/json</value>
      </set-header>
      <set-body>{cdata(machines)}</set-body>
    </return-response>
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
""".strip()

# Policy for GET /machines/{id} (choose per id)
when_blocks = []
for m in machines:
    mid = m.get("id")
    if not mid:
        continue
    when_blocks.append(f"""
      <when condition="@((string)context.Request.MatchedParameters[&quot;id&quot;] == &quot;{mid}&quot;)">
        <return-response>
          <set-status code="200" reason="OK" />
          <set-header name="Content-Type" exists-action="override">
            <value>application/json</value>
          </set-header>
          <set-body>{cdata(m)}</set-body>
        </return-response>
      </when>
    """.rstrip())

policy_by_id = f"""
<policies>
  <inbound>
    <base />
    <choose>
      {''.join(when_blocks)}
      <otherwise>
        <return-response>
          <set-status code="404" reason="Not Found" />
          <set-header name="Content-Type" exists-action="override">
            <value>application/json</value>
          </set-header>
          <set-body><![CDATA[{{"error":"machine not found"}}]]></set-body>
        </return-response>
      </otherwise>
    </choose>
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
""".strip()

# Create client using Azure CLI auth
cred = AzureCliCredential()
client = ApiManagementClient(cred, sub_id)

# Create or update API container (LRO)
client.api.begin_create_or_update(
    rg, service, api_id,
    ApiCreateOrUpdateParameter(
        display_name="Machine API",
        description="Mocked Machine data via APIM policies",
        path="machine",
        protocols=[Protocol.https],
        subscription_required=True
    )
).result()

# Operation: GET /machines
client.api_operation.create_or_update(
    rg, service, api_id, "get-machines",
    OperationContract(
        display_name="List Machines",
        description="Retrieve all machines",
        method="GET",
        url_template="/",
        template_parameters=[],
        responses=[ResponseContract(status_code=200, description="OK")]
    )
)

# Attach policy for GET /machines
client.api_operation_policy.create_or_update(
  rg, service, api_id, "get-machines", "policy",
  parameters=PolicyContract(value=policy_all, format="rawxml")
)

# Operation: GET /machines/{id}
client.api_operation.create_or_update(
    rg, service, api_id, "get-machine",
    OperationContract(
        display_name="Get Machine",
        description="Retrieve machine by ID",
        method="GET",
        url_template="/{id}",
        template_parameters=[ParameterContract(name="id", type="string", required=True)],
        responses=[
            ResponseContract(status_code=200, description="OK"),
            ResponseContract(status_code=404, description="Not Found")
        ]
    )
)

# Attach policy for GET /machines/{id}
client.api_operation_policy.create_or_update(
  rg, service, api_id, "get-machine", "policy",
  parameters=PolicyContract(value=policy_by_id, format="rawxml")
)

print("✅ APIM mock API deployed: path=/machine with operations / and /{id}")
EOF

echo "🐍 Running APIM mock script..."
python3 seed_apim_mock.py

echo "🧹 Cleaning up..."
rm -f seed_apim_mock.py

echo "✅ APIM seeding complete!"