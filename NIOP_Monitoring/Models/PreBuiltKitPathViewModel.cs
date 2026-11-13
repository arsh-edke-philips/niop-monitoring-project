namespace NIOP_Monitoring.Models
{
    public class PreBuiltKitPathViewModel
    {
        public int PBKSerialNumber { get; set; }
        public string PBKStatus { get; set; } = "";
        public string Center { get; set; } = "";
        public int? BundleNumber { get; set; }
        public List<string> PartNumbers { get; set; } = new List<string>();
        public DateTime ExpirationDate { get; set; }
        public string Kit { get; set; } = "";
        public OrderLineagePathViewModel? Order { get; set; }
    }
}
