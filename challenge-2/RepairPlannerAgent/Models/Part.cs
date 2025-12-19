namespace RepairPlannerAgent.Models
{
    /// <summary>
    /// Represents an inventory part that may be required for equipment repair.
    /// </summary>
    public class Part
    {
        /// <summary>
        /// Gets or sets the unique part number identifier.
        /// </summary>
        public string PartNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the part.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity of this part currently available in inventory.
        /// </summary>
        public int QuantityAvailable { get; set; }

        /// <summary>
        /// Gets or sets the warehouse or storage location of the part.
        /// </summary>
        public string Location { get; set; } = string.Empty;
    }
}
