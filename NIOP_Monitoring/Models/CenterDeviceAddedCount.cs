using Microsoft.Extensions.Primitives;

namespace NIOP_Monitoring.Models
{
    public class CenterDeviceAddedCount
    {
        public Guid Id { get; set; }
        public string CenterName { get; set; } = "";
        public int TodayCount { get; set; }
        public int LastWeekCount { get; set; }
        public int LastMonthCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class LocationDeviceCount
    {
        public string Location { get; set; } = "";
        public int Count { get; set; } = 0;
    }

    public class LocationDeviceCountPerCenter
    {
        public Guid CenterId { get; set; }
        public string CenterName { get; set; } = "";
        public List<LocationDeviceCount> LocationCounts { get; set; } = new();
    }
}
