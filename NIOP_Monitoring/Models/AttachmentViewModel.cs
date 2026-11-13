namespace NIOP_Monitoring.Models
{
    public class AttachmentViewModel
    {
        public Guid AttachmentId { get; set; }
        public string ContentType { get; set; } = "";
        public string AttachmentType { get; set; } = "";
    }
}
