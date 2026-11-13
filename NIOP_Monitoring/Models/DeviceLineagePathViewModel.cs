namespace NIOP_Monitoring.Models
{
    public class DeviceLineagePathViewModel
    {
        public string Device { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public string DeviceCategory { get; set; } = "";
        public string DeviceConfig { get; set; } = "";
        public string Part { get; set; } = "";
        public Guid? Bundle { get; set; }
        public Guid? Order { get; set; }
        public string Location { get; set; } = "";
        public string Center { get; set; } = "";
        public string? Facility { get; set; }
        public string? PurchaseOrderNumber { get; set; }

    }
}
