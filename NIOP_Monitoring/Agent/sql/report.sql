SELECT
    r.Number       AS ReportNumber,
    rs.Name        AS ReportStatus,
    rt.Name        AS ReportType,
    r.CreatedDate  AS CreatedDate,
    r.OnHold,
    r.OnHoldBy,
    r.OnHoldReason,
    a.Id           AS AttachmentId,
    at.Name        AS AttachmentType,
    ot.Name        AS ObjectType,
    a.ContentType,
    e.Id           AS EnrollmentId,
    e.Number       AS EnrollmentNumber,
    es.Name        AS EnrollmentStatus,
    dt.Name        AS DeviceType,
    f.Name         AS Facility
FROM dbo.Report r
LEFT JOIN dbo.ReportStatus     rs ON rs.Id = r.ReportStatusId
LEFT JOIN dbo.ReportType       rt ON rt.Id = r.ReportTypeId
LEFT JOIN dbo.Facility         f  ON f.Id  = r.FacilityId
LEFT JOIN dbo.Enrollment       e  ON e.Id  = r.EnrollmentId
LEFT JOIN dbo.EnrollmentStatus es ON es.Id = e.StatusId
LEFT JOIN dbo.DeviceType       dt ON dt.Id = e.DeviceTypeId
LEFT JOIN dbo.Attachment       a  ON a.Id  = r.ReportFileId
LEFT JOIN dbo.AttachmentType   at ON at.Id = a.AttachmentTypeId
LEFT JOIN dbo.ObjectType       ot ON ot.Id = a.ObjectTypeId