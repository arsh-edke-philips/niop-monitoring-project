namespace NIOP_Monitoring.Models
{
    public class OrderLineagePathViewModel
    {
        public int OrderNumber { get; set; }
        public string Kit { get; set; } = "";
        public Guid? PreBuiltKit { get; set; }
        public Guid? Bundle { get; set; }
        public Guid? Device { get; set; }
        public string? Facility { get; set; }
        public string Center { get; set; } = "";
        public string OrderType { get; set; } = "";
        public string OrderStatus { get; set; } = "";
        public string CreatedUser { get; set; } = "";       

    }
}
