using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlannerAgent.Models;
using System.Net;

namespace RepairPlannerAgent.Services
{
    /// <summary>
    /// Service for interacting with Azure Cosmos DB to manage technicians, parts, and work orders.
    /// </summary>
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Container _techniciansContainer;
        private readonly Container _partsContainer;
        private readonly Container _machinesContainer;
        private readonly Container _workOrdersContainer;
        private readonly ILogger<CosmosDbService> _logger;

        /// <summary>
        /// Initializes a new instance of the CosmosDbService class.
        /// </summary>
        /// <param name="endpoint">The Cosmos DB endpoint URL.</param>
        /// <param name="key">The Cosmos DB access key.</param>
        /// <param name="databaseName">The name of the database to use.</param>
        /// <param name="logger">The logger instance for logging operations.</param>
        public CosmosDbService(string endpoint, string key, string databaseName, ILogger<CosmosDbService> logger)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Cosmos DB endpoint cannot be null or empty", nameof(endpoint));
            
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cosmos DB key cannot be null or empty", nameof(key));
            
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("Database name cannot be null or empty", nameof(databaseName));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                _client = new CosmosClient(endpoint, key);
                var database = _client.GetDatabase(databaseName);
                
                _techniciansContainer = database.GetContainer("Technicians");
                _partsContainer = database.GetContainer("PartsInventory");
                _machinesContainer = database.GetContainer("Machines");
                _workOrdersContainer = database.GetContainer("WorkOrders");

                _logger.LogInformation("CosmosDbService initialized successfully for database: {DatabaseName}", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize CosmosDbService");
                throw;
            }
        }

        /// <summary>
        /// Queries available technicians who have at least one of the required skills.
        /// </summary>
        /// <param name="requiredSkills">List of skills required for the repair task.</param>
        /// <returns>A list of available technicians with matching skills.</returns>
        public async Task<List<Technician>> GetAvailableTechniciansWithSkillsAsync(List<string> requiredSkills)
        {
            if (requiredSkills == null || !requiredSkills.Any())
            {
                _logger.LogWarning("GetAvailableTechniciansWithSkillsAsync called with empty or null required skills list");
                return new List<Technician>();
            }

            try
            {
                _logger.LogInformation("Querying technicians with skills: {Skills}", string.Join(", ", requiredSkills));

                // Build the query to find technicians with matching skills who are available
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.Available = true AND ARRAY_LENGTH(ARRAY_INTERSECT(c.Skills, @requiredSkills)) > 0"
                )
                .WithParameter("@requiredSkills", requiredSkills);

                var technicians = new List<Technician>();
                using var iterator = _techniciansContainer.GetItemQueryIterator<Technician>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    technicians.AddRange(response);
                    
                    _logger.LogDebug("Retrieved {Count} technicians in this batch. RU charge: {RequestCharge}", 
                        response.Count, response.RequestCharge);
                }

                // Sort by experience years (most experienced first)
                var sortedTechnicians = technicians
                    .OrderByDescending(t => t.ExperienceYears)
                    .ToList();

                _logger.LogInformation("Found {Count} available technicians with required skills", sortedTechnicians.Count);
                return sortedTechnicians;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Technicians container not found");
                return new List<Technician>();
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while querying technicians. Status: {Status}, Message: {Message}", 
                    ex.StatusCode, ex.Message);
                throw new Exception($"Failed to query technicians from Cosmos DB: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while querying technicians");
                throw;
            }
        }

        /// <summary>
        /// Fetches parts inventory by specific part numbers.
        /// </summary>
        /// <param name="partNumbers">List of part numbers to retrieve.</param>
        /// <returns>A list of parts matching the provided part numbers.</returns>
        public async Task<List<Part>> GetPartsInventoryAsync(List<string> partNumbers)
        {
            if (partNumbers == null || !partNumbers.Any())
            {
                _logger.LogWarning("GetPartsInventoryAsync called with empty or null part numbers list");
                return new List<Part>();
            }

            try
            {
                _logger.LogInformation("Querying parts inventory for part numbers: {PartNumbers}", 
                    string.Join(", ", partNumbers));

                // Build the query to find parts by part numbers
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE ARRAY_CONTAINS(@partNumbers, c.PartNumber)"
                )
                .WithParameter("@partNumbers", partNumbers);

                var parts = new List<Part>();
                using var iterator = _partsContainer.GetItemQueryIterator<Part>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    parts.AddRange(response);
                    
                    _logger.LogDebug("Retrieved {Count} parts in this batch. RU charge: {RequestCharge}", 
                        response.Count, response.RequestCharge);
                }

                // Log any missing parts
                var foundPartNumbers = parts.Select(p => p.PartNumber).ToList();
                var missingPartNumbers = partNumbers.Except(foundPartNumbers).ToList();
                
                if (missingPartNumbers.Any())
                {
                    _logger.LogWarning("Parts not found in inventory: {MissingParts}", 
                        string.Join(", ", missingPartNumbers));
                }

                // Log parts with zero quantity
                var unavailableParts = parts.Where(p => p.QuantityAvailable <= 0).ToList();
                if (unavailableParts.Any())
                {
                    _logger.LogWarning("Parts with zero quantity: {UnavailableParts}", 
                        string.Join(", ", unavailableParts.Select(p => p.PartNumber)));
                }

                _logger.LogInformation("Found {Count} parts out of {Requested} requested", 
                    parts.Count, partNumbers.Count);
                
                return parts;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Parts inventory container not found");
                return new List<Part>();
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while querying parts. Status: {Status}, Message: {Message}", 
                    ex.StatusCode, ex.Message);
                throw new Exception($"Failed to query parts from Cosmos DB: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while querying parts inventory");
                throw;
            }
        }

        /// <summary>
        /// Creates a new work order in the Cosmos DB database.
        /// </summary>
        /// <param name="workOrder">The work order to create.</param>
        /// <returns>The ID of the created work order.</returns>
        public async Task<string> CreateWorkOrderAsync(WorkOrder workOrder)
        {
            if (workOrder == null)
                throw new ArgumentNullException(nameof(workOrder));

            if (string.IsNullOrWhiteSpace(workOrder.Id))
            {
                workOrder.Id = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated new work order ID: {WorkOrderId}", workOrder.Id);
            }

            try
            {
                _logger.LogInformation("Creating work order {WorkOrderId} for machine {MachineId}", 
                    workOrder.Id, workOrder.MachineId);

                var response = await _workOrdersContainer.CreateItemAsync(
                    workOrder,
                    new PartitionKey(workOrder.Id)
                );

                _logger.LogInformation(
                    "Work order {WorkOrderId} created successfully. RU charge: {RequestCharge}", 
                    workOrder.Id, 
                    response.RequestCharge
                );

                return workOrder.Id;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogError("Work order {WorkOrderId} already exists", workOrder.Id);
                throw new InvalidOperationException($"Work order with ID {workOrder.Id} already exists", ex);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, 
                    "Cosmos DB error while creating work order {WorkOrderId}. Status: {Status}, Message: {Message}", 
                    workOrder.Id, ex.StatusCode, ex.Message);
                throw new Exception($"Failed to create work order in Cosmos DB: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating work order {WorkOrderId}", workOrder.Id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a work order by its ID.
        /// </summary>
        /// <param name="workOrderId">The ID of the work order to retrieve.</param>
        /// <returns>The work order if found, null otherwise.</returns>
        public async Task<WorkOrder?> GetWorkOrderByIdAsync(string workOrderId)
        {
            if (string.IsNullOrWhiteSpace(workOrderId))
                throw new ArgumentException("Work order ID cannot be null or empty", nameof(workOrderId));

            try
            {
                _logger.LogInformation("Retrieving work order: {WorkOrderId}", workOrderId);

                var response = await _workOrdersContainer.ReadItemAsync<WorkOrder>(
                    workOrderId,
                    new PartitionKey(workOrderId)
                );

                _logger.LogInformation("Work order {WorkOrderId} retrieved successfully", workOrderId);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Work order {WorkOrderId} not found", workOrderId);
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while retrieving work order {WorkOrderId}", workOrderId);
                throw new Exception($"Failed to retrieve work order from Cosmos DB: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrieving work order {WorkOrderId}", workOrderId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing work order status.
        /// </summary>
        /// <param name="workOrderId">The ID of the work order to update.</param>
        /// <param name="newStatus">The new status to set.</param>
        /// <returns>True if the update was successful, false if the work order was not found.</returns>
        public async Task<bool> UpdateWorkOrderStatusAsync(string workOrderId, string newStatus)
        {
            if (string.IsNullOrWhiteSpace(workOrderId))
                throw new ArgumentException("Work order ID cannot be null or empty", nameof(workOrderId));

            if (string.IsNullOrWhiteSpace(newStatus))
                throw new ArgumentException("Status cannot be null or empty", nameof(newStatus));

            try
            {
                _logger.LogInformation("Updating work order {WorkOrderId} status to: {Status}", 
                    workOrderId, newStatus);

                var workOrder = await GetWorkOrderByIdAsync(workOrderId);
                if (workOrder == null)
                {
                    _logger.LogWarning("Cannot update work order {WorkOrderId} - not found", workOrderId);
                    return false;
                }

                workOrder.Status = newStatus;

                await _workOrdersContainer.ReplaceItemAsync(
                    workOrder,
                    workOrderId,
                    new PartitionKey(workOrderId)
                );

                _logger.LogInformation("Work order {WorkOrderId} status updated successfully", workOrderId);
                return true;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while updating work order {WorkOrderId} status", workOrderId);
                throw new Exception($"Failed to update work order status in Cosmos DB: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating work order {WorkOrderId} status", workOrderId);
                throw;
            }
        }
    }
}
