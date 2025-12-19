namespace RepairPlannerAgent.Models
{
    /// <summary>
    /// Represents a work order for equipment repair with all required resources and scheduling.
    /// This is the output of the repair planning process.
    /// </summary>
    public class WorkOrder
    {
        /// <summary>
        /// Gets or sets the unique identifier for this work order.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the machine identifier that requires repair.
        /// </summary>
        public string MachineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of fault being addressed.
        /// </summary>
        public string FaultType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of repair tasks to be performed.
        /// </summary>
        public List<RepairTask> Tasks { get; set; } = new List<RepairTask>();

        /// <summary>
        /// Gets or sets the technician assigned to perform the repair.
        /// </summary>
        public Technician? AssignedTechnician { get; set; }

        /// <summary>
        /// Gets or sets the list of parts required for the repair.
        /// </summary>
        public List<Part> RequiredParts { get; set; } = new List<Part>();

        /// <summary>
        /// Gets or sets the scheduled start time for the maintenance window.
        /// </summary>
        public DateTime ScheduledStart { get; set; }

        /// <summary>
        /// Gets or sets the estimated duration of the repair in minutes.
        /// </summary>
        public int EstimatedDurationMinutes { get; set; }

        /// <summary>
        /// Gets or sets the priority level of this work order (e.g., "Critical", "High", "Medium", "Low").
        /// </summary>
        public string Priority { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the work order (e.g., "Scheduled", "In Progress", "Completed").
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an individual repair task within a work order.
    /// </summary>
    public class RepairTask
    {
        /// <summary>
        /// Gets or sets the detailed description of the task to be performed.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the estimated time to complete this task in minutes.
        /// </summary>
        public int EstimatedMinutes { get; set; }

        /// <summary>
        /// Gets or sets the list of tools required to perform this task.
        /// </summary>
        public List<string> RequiredTools { get; set; } = new List<string>();
    }
}
