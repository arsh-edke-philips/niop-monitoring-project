namespace NIOP_Monitoring.Models
{
    public class ReportPathViewModel
    {
        public int ReportNumber { get; set; }
        public string ReportStatus { get; set; } = "";
        public string ReportType { get; set; } = "";
        public int OnHold { get; set; }
        public string OnHoldBy { get; set; } = "";
        public string OnHoldReason { get; set; } = "";
        public AttachmentViewModel? Attachment { get; set; }
        public EnrollmentPathViewModel Enrollment { get; set; } = new EnrollmentPathViewModel();
    }
}
