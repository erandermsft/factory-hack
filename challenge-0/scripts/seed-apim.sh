#!/usr/bin/env bash
set -e

# Load environment variables from .env in parent directory
if [ -f ../.env ]; then
    set -a
    source ../.env
    set +a
    echo "✅ Loaded environment variables from ../.env"
else
    echo "⚠️ .env file not found. Make sure AZURE_SUBSCRIPTION_ID, RESOURCE_GROUP, APIM_NAME and COSMOS_ENDPOINT are set in the environment."
fi

# Validate required env vars
: "${AZURE_SUBSCRIPTION_ID:?Missing AZURE_SUBSCRIPTION_ID}"
: "${RESOURCE_GROUP:?Missing RESOURCE_GROUP}"
: "${APIM_NAME:?Missing APIM_NAME}"
: "${COSMOS_ENDPOINT:?Missing COSMOS_ENDPOINT}"

echo "📦 Installing Azure SDKs for APIM..."
pip3 install azure-identity azure-mgmt-apimanagement==4.0.0 --quiet

echo "📝 Generating APIM setup script (Cosmos via Managed Identity)..."
cat > seed_apim_cosmos_mi.py << 'EOF'
import os
from urllib.parse import urlparse
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
cosmos_endpoint = os.environ.get("COSMOS_ENDPOINT")  # e.g. https://<account>.documents.azure.com/
api_id = "machine-api"

missing = [k for k,v in {"AZURE_SUBSCRIPTION_ID":sub_id, "RESOURCE_GROUP":rg, "APIM_NAME":service, "COSMOS_ENDPOINT": cosmos_endpoint}.items() if not v]
if missing:
    raise RuntimeError(f"Missing environment variables: {', '.join(missing)}")

# Parse and normalize Cosmos endpoint
parsed = urlparse(cosmos_endpoint)
cosmos_endpoint = f"{parsed.scheme}://{parsed.hostname}/"
resource_attr = f"{parsed.scheme}://{parsed.hostname}"  # MI resource must be origin without port, slash, or path

print(f"ℹ️  Cosmos endpoint: {cosmos_endpoint}")
print(f"ℹ️  MI resource: {resource_attr}")

# Policy for GET /machines (List all machines from Cosmos DB using AAD token)
policy_list = f"""
<policies>
  <inbound>
    <base />
    <set-variable name="requestDateString" value="@(DateTime.UtcNow.ToString(&quot;r&quot;))" />
    <authentication-managed-identity resource="{resource_attr}" output-token-variable-name="msi-access-token" ignore-error="false" />
    <send-request mode="new" response-variable-name="cosmosResponse" timeout="30">
      <set-url>@("{cosmos_endpoint}" + "dbs/FactoryOpsDB/colls/Machines/docs")</set-url>
      <set-method>POST</set-method>
      <set-header name="Authorization" exists-action="override">
        <value>@("type=aad&amp;ver=1.0&amp;sig=" + (string)context.Variables["msi-access-token"])</value>
      </set-header>
      <set-header name="x-ms-date" exists-action="override">
        <value>@(context.Variables.GetValueOrDefault&lt;string&gt;("requestDateString"))</value>
      </set-header>
      <set-header name="x-ms-version" exists-action="override"><value>2018-12-31</value></set-header>
      <set-header name="x-ms-documentdb-isquery" exists-action="override"><value>true</value></set-header>
      <set-header name="x-ms-documentdb-query-enablecrosspartition" exists-action="override"><value>true</value></set-header>
      <set-header name="Content-Type" exists-action="override"><value>application/query+json</value></set-header>
      <set-header name="Accept" exists-action="override"><value>application/json</value></set-header>
      <set-body>@{{
        return JsonConvert.SerializeObject(new {{
          query = "SELECT * FROM c",
          parameters = new object[0]
        }});
      }}</set-body>
    </send-request>
    <choose>
      <when condition="@(((IResponse)context.Variables[&quot;cosmosResponse&quot;]).StatusCode == 200)">
        <return-response>
          <set-status code="200" reason="OK" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{
            var response = ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;JObject&gt;();
            return response["Documents"].ToString();
          }}</set-body>
        </return-response>
      </when>
      <otherwise>
        <return-response>
          <set-status code="502" reason="Cosmos DB Query Failed" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{ return ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;string&gt;(); }}</set-body>
        </return-response>
      </otherwise>
    </choose>
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
""".strip()

# Policy for GET /machines/{id} (single machine)
policy_get = f"""
<policies>
  <inbound>
    <base />
    <set-variable name="requestDateString" value="@(DateTime.UtcNow.ToString(&quot;r&quot;))" />
    <authentication-managed-identity resource="{resource_attr}" output-token-variable-name="msi-access-token" ignore-error="false" />
    <set-variable name="machineId" value="@(context.Request.MatchedParameters[&quot;id&quot;])" />
    <send-request mode="new" response-variable-name="cosmosResponse" timeout="30">
      <set-url>@("{cosmos_endpoint}" + "dbs/FactoryOpsDB/colls/Machines/docs")</set-url>
      <set-method>POST</set-method>
      <set-header name="Authorization" exists-action="override">
        <value>@("type=aad&amp;ver=1.0&amp;sig=" + (string)context.Variables["msi-access-token"])</value>
      </set-header>
      <set-header name="x-ms-date" exists-action="override">
        <value>@(context.Variables.GetValueOrDefault&lt;string&gt;("requestDateString"))</value>
      </set-header>
      <set-header name="x-ms-version" exists-action="override"><value>2018-12-31</value></set-header>
      <set-header name="x-ms-documentdb-isquery" exists-action="override"><value>true</value></set-header>
      <set-header name="x-ms-documentdb-query-enablecrosspartition" exists-action="override"><value>true</value></set-header>
      <set-header name="Content-Type" exists-action="override"><value>application/query+json</value></set-header>
      <set-header name="Accept" exists-action="override"><value>application/json</value></set-header>
      <set-body>@{{
        string machineId = context.Variables["machineId"] as string;
        return JsonConvert.SerializeObject(new {{
          query = "SELECT * FROM c WHERE c.id = @id",
          parameters = new object[] {{ new {{ name = "@id", value = machineId }} }}
        }});
      }}</set-body>
    </send-request>
    <choose>
      <when condition="@(((IResponse)context.Variables[&quot;cosmosResponse&quot;]).StatusCode == 200)">
        <return-response>
          <set-status code="200" reason="OK" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{
            var response = ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;JObject&gt;();
            var docs = response["Documents"] as JArray;
            return docs.Count > 0 ? docs[0].ToString() : JsonConvert.SerializeObject(new {{ error = "machine not found" }});
          }}</set-body>
        </return-response>
      </when>
      <otherwise>
        <return-response>
          <set-status code="502" reason="Cosmos DB Query Failed" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{ return ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;string&gt;(); }}</set-body>
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

# Create or update API container
print("📡 Creating Machine API...")
client.api.begin_create_or_update(
    rg, service, api_id,
    ApiCreateOrUpdateParameter(
        display_name="Machine API",
        description="Machines via Cosmos DB (APIM Managed Identity)",
        path="machine",
        protocols=[Protocol.https],
        subscription_required=True
    )
).result()

# Operation: GET /machines
print("📡 Creating List Machines operation...")
client.api_operation.create_or_update(
    rg, service, api_id, "list-machines",
    OperationContract(
        display_name="List Machines",
        description="Retrieves all machines from the factory operations database. Returns an array of machine objects with their configurations, status, and metadata.",
        method="GET",
        url_template="/",
        template_parameters=[],
        responses=[ResponseContract(status_code=200, description="OK")]
    )
)
client.api_operation_policy.create_or_update(
  rg, service, api_id, "list-machines", "policy",
  parameters=PolicyContract(value=policy_list, format="rawxml")
)

# Operation: GET /machines/{id}
print("📡 Creating Get Machine operation...")
client.api_operation.create_or_update(
    rg, service, api_id, "get-machine",
    OperationContract(
        display_name="Get Machine",
        description="Retrieves a specific machine by its unique identifier. Returns detailed information about the machine including type, configuration, location, and current operational status.",
        method="GET",
        url_template="/{id}",
        template_parameters=[ParameterContract(name="id", type="string", required=True)],
        responses=[
            ResponseContract(status_code=200, description="OK"),
            ResponseContract(status_code=404, description="Not Found")
        ]
    )
)
client.api_operation_policy.create_or_update(
  rg, service, api_id, "get-machine", "policy",
  parameters=PolicyContract(value=policy_get, format="rawxml")
)

print("✅ APIM Machine API deployed: path=/machine (Cosmos via Managed Identity)")

# ============================================================================
# Maintenance API - Thresholds from Cosmos DB
# ============================================================================

api_id_maintenance = "maintenance-api"

# Policy for GET /thresholds (List all thresholds)
policy_list_thresholds = f"""
<policies>
  <inbound>
    <base />
    <set-variable name="requestDateString" value="@(DateTime.UtcNow.ToString(&quot;r&quot;))" />
    <authentication-managed-identity resource="{resource_attr}" output-token-variable-name="msi-access-token" ignore-error="false" />
    <send-request mode="new" response-variable-name="cosmosResponse" timeout="30">
      <set-url>@("{cosmos_endpoint}" + "dbs/FactoryOpsDB/colls/Thresholds/docs")</set-url>
      <set-method>POST</set-method>
      <set-header name="Authorization" exists-action="override">
        <value>@("type=aad&amp;ver=1.0&amp;sig=" + (string)context.Variables["msi-access-token"])</value>
      </set-header>
      <set-header name="x-ms-date" exists-action="override">
        <value>@(context.Variables.GetValueOrDefault&lt;string&gt;("requestDateString"))</value>
      </set-header>
      <set-header name="x-ms-version" exists-action="override"><value>2018-12-31</value></set-header>
      <set-header name="x-ms-documentdb-isquery" exists-action="override"><value>true</value></set-header>
      <set-header name="x-ms-documentdb-query-enablecrosspartition" exists-action="override"><value>true</value></set-header>
      <set-header name="Content-Type" exists-action="override"><value>application/query+json</value></set-header>
      <set-header name="Accept" exists-action="override"><value>application/json</value></set-header>
      <set-body>@{{
        return JsonConvert.SerializeObject(new {{
          query = "SELECT * FROM c",
          parameters = new object[0]
        }});
      }}</set-body>
    </send-request>
    <choose>
      <when condition="@(((IResponse)context.Variables[&quot;cosmosResponse&quot;]).StatusCode == 200)">
        <return-response>
          <set-status code="200" reason="OK" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{
            var response = ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;JObject&gt;();
            return response["Documents"].ToString();
          }}</set-body>
        </return-response>
      </when>
      <otherwise>
        <return-response>
          <set-status code="502" reason="Cosmos DB Query Failed" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{ return ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;string&gt;(); }}</set-body>
        </return-response>
      </otherwise>
    </choose>
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
""".strip()

# Policy for GET /thresholds/{machineType} (thresholds by machine type)
policy_get_thresholds = f"""
<policies>
  <inbound>
    <base />
    <set-variable name="requestDateString" value="@(DateTime.UtcNow.ToString(&quot;r&quot;))" />
    <authentication-managed-identity resource="{resource_attr}" output-token-variable-name="msi-access-token" ignore-error="false" />
    <set-variable name="machineType" value="@(context.Request.MatchedParameters[&quot;machineType&quot;])" />
    <send-request mode="new" response-variable-name="cosmosResponse" timeout="30">
      <set-url>@("{cosmos_endpoint}" + "dbs/FactoryOpsDB/colls/Thresholds/docs")</set-url>
      <set-method>POST</set-method>
      <set-header name="Authorization" exists-action="override">
        <value>@("type=aad&amp;ver=1.0&amp;sig=" + (string)context.Variables["msi-access-token"])</value>
      </set-header>
      <set-header name="x-ms-date" exists-action="override">
        <value>@(context.Variables.GetValueOrDefault&lt;string&gt;("requestDateString"))</value>
      </set-header>
      <set-header name="x-ms-version" exists-action="override"><value>2018-12-31</value></set-header>
      <set-header name="x-ms-documentdb-isquery" exists-action="override"><value>true</value></set-header>
      <set-header name="x-ms-documentdb-query-enablecrosspartition" exists-action="override"><value>true</value></set-header>
      <set-header name="Content-Type" exists-action="override"><value>application/query+json</value></set-header>
      <set-header name="Accept" exists-action="override"><value>application/json</value></set-header>
      <set-body>@{{
        string machineType = context.Variables["machineType"] as string;
        return JsonConvert.SerializeObject(new {{
          query = "SELECT * FROM c WHERE c.machineType = @machineType",
          parameters = new object[] {{ new {{ name = "@machineType", value = machineType }} }}
        }});
      }}</set-body>
    </send-request>
    <choose>
      <when condition="@(((IResponse)context.Variables[&quot;cosmosResponse&quot;]).StatusCode == 200)">
        <return-response>
          <set-status code="200" reason="OK" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{
            var response = ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;JObject&gt;();
            return response["Documents"].ToString();
          }}</set-body>
        </return-response>
      </when>
      <otherwise>
        <return-response>
          <set-status code="502" reason="Cosmos DB Query Failed" />
          <set-header name="Content-Type" exists-action="override"><value>application/json</value></set-header>
          <set-body>@{{ return ((IResponse)context.Variables["cosmosResponse"]).Body.As&lt;string&gt;(); }}</set-body>
        </return-response>
      </otherwise>
    </choose>
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
""".strip()

# Create or update Maintenance API container
print("📡 Creating Maintenance API...")
client.api.begin_create_or_update(
    rg, service, api_id_maintenance,
    ApiCreateOrUpdateParameter(
        display_name="Maintenance API",
        description="Thresholds via Cosmos DB (APIM Managed Identity)",
        path="maintenance",
        protocols=[Protocol.https],
        subscription_required=True
    )
).result()

# Operation: GET /maintenance (List all thresholds)
print("📡 Creating List Thresholds operation...")
client.api_operation.create_or_update(
    rg, service, api_id_maintenance, "list-thresholds",
    OperationContract(
        display_name="List Thresholds",
        description="Retrieves all operational thresholds for factory equipment. Returns threshold configurations including normal ranges, warning levels, and critical thresholds for various machine types and metrics.",
        method="GET",
        url_template="/",
        template_parameters=[],
        responses=[ResponseContract(status_code=200, description="OK")]
    )
)
client.api_operation_policy.create_or_update(
  rg, service, api_id_maintenance, "list-thresholds", "policy",
  parameters=PolicyContract(value=policy_list_thresholds, format="rawxml")
)

# Operation: GET /maintenance/{machineType} (thresholds by machine type)
print("📡 Creating Get Threshold operation...")
client.api_operation.create_or_update(
    rg, service, api_id_maintenance, "get-threshold",
    OperationContract(
        display_name="Get Threshold",
        description="Retrieves operational thresholds for a specific machine type (e.g., tire_curing_press, banbury_mixer). Returns all threshold configurations applicable to the specified machine type including temperature, pressure, vibration, and other critical metrics.",
        method="GET",
        url_template="/{machineType}",
        template_parameters=[ParameterContract(name="machineType", type="string", required=True)],
        responses=[
            ResponseContract(status_code=200, description="OK"),
            ResponseContract(status_code=404, description="Not Found")
        ]
    )
)
client.api_operation_policy.create_or_update(
  rg, service, api_id_maintenance, "get-threshold", "policy",
  parameters=PolicyContract(value=policy_get_thresholds, format="rawxml")
)

print("✅ APIM Maintenance API deployed: path=/maintenance (Cosmos via Managed Identity)")
EOF

echo "🐍 Running APIM setup (Managed Identity to Cosmos)..."
python3 seed_apim_cosmos_mi.py

echo "🧹 Cleaning up..."
rm -f seed_apim_cosmos_mi.py

echo "✅ APIM seeding complete!"
