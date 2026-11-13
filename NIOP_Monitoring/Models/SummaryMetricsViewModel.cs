namespace NIOP_Monitoring.Models
{
    public class SummaryMetricsViewModel
    {
        public int OrdersCreatedToday { get; set; }
        public int OrdersCreatedThisWeek { get; set; }
        public int OrdersCreatedThisMonth { get; set; }
        public int PendingOrders { get; set; }
        public decimal ShippedWithinDeliveryDatePercentage { get; set; }
        public int DevicesTestedToday { get; set; }
        public int FaultBacklog { get; set; }
    }
}
