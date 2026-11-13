namespace NIOP_Monitoring.Models
{
    public class TransferPathViewModel
    {
        public int TransferNumber { get; set; }
        public string TransferStatus { get; set; } = "";
        public string FromCenter { get; set; } = "";
        public string ToCenter { get; set; } = "";
        public string Note { get; set; } = "";
        public List<DeviceLineagePathViewModel> Device { get; set; } = new();
    }
}
