namespace NIOP_Monitoring.Models
{
    public class EnrollmentPathViewModel
    {
        public int EnrollmentNumber { get; set; }
        public string EnrollmentStatus { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public string BillingMethod { get; set; } = "";
        public string? BillingStatus { get; set; }
        public int PendingAttachmentCount { get; set; }
        public int OrderNumber { get; set; }
        public string Facility { get; set; } = "";
        public List<AttachmentViewModel> Attachment { get; set; } = new();
        public ReportPathViewModel? Report { get; set; }
    }
}
