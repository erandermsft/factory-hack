namespace RepairPlannerAgent.Models
{
    /// <summary>
    /// Represents a diagnosed fault from the predictive maintenance agent.
    /// This is the input to the repair planning process.
    /// </summary>
    public class DiagnosedFault
    {
        /// <summary>
        /// Gets or sets the unique identifier of the machine experiencing the fault.
        /// </summary>
        public string MachineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of fault detected (e.g., "Overheating", "Vibration", "Pressure Drop").
        /// </summary>
        public string FaultType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the root cause analysis of the fault.
        /// </summary>
        public string RootCause { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the severity level of the fault (e.g., "Critical", "High", "Medium", "Low").
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the fault was detected.
        /// </summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the fault including telemetry values.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
