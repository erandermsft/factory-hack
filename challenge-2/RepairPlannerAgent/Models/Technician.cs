namespace RepairPlannerAgent.Models
{
    /// <summary>
    /// Represents a maintenance technician with their skills and availability.
    /// </summary>
    public class Technician
    {
        /// <summary>
        /// Gets or sets the unique identifier of the technician.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the technician.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of skills the technician possesses (e.g., "HVAC Systems", "Electrical", "Mechanical").
        /// </summary>
        public List<string> Skills { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether the technician is currently available for work assignments.
        /// </summary>
        public bool Available { get; set; }

        /// <summary>
        /// Gets or sets the number of years of experience the technician has.
        /// </summary>
        public int ExperienceYears { get; set; }
    }
}
