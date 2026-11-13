namespace NIOP_Monitoring.Models
{
    public class DeviceTypeCountViewModel
    {
        public string DeviceType { get; set; } = "";
        public int Count { get; set; }
    }

    public class DeviceCountByDistributionCenterViewModel
    {
        public string DistributionCenter { get; set; } = "";
        public List<DeviceTypeCountViewModel> DeviceTypeCounts { get; set; } = new();
    }
}
