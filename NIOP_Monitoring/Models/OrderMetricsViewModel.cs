namespace NIOP_Monitoring.Models
{
    public class OrderMetricsViewModel
    {
        public int PendingOrders { get; set; }
        public List<DistributionCenterPendingViewModel> PendingByDistributionCenter { get; set; } = new();
        public decimal ShippedWithinDeliveryDatePercentage { get; set; }
        public decimal AvgFulfillmentDurationDays { get; set; }
        public int DelayedOrders { get; set; }
        public int CancelledOrderVolume { get; set; }
    }
}
