namespace NIOP_Monitoring.Models
{
    public class OperationsDashboardViewModel
    {
        public SummaryMetricsViewModel Summary { get; set; } = new();
        public OrderMetricsViewModel Orders { get; set; } = new();
        public DeviceMetricsViewModel Devices { get; set; } = new();
        public List<DeviceLineagePathViewModel> DevicesList { get; set; } = new();
        public List<OrderLineagePathViewModel> OrdersList { get; set; } = new();
        public List<CenterDeviceAddedCount> DeviceCounts { get; set; } = new();
        public List<LocationDeviceCountPerCenter> LocationDeviceCounts { get; set; } = new();
        public List<EnrollmentPathViewModel> EnrollmentsList { get; set; } = new();
        public List<ReportPathViewModel> ReportsList { get; set; } = new();
        public List<PreBuiltKitPathViewModel> PreBuiltKitPaths { get; set;} = new();
        public List<TransferPathViewModel> TransferPaths { get; set; } = new();
    }
}
