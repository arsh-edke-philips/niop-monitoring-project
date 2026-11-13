SELECT
    e.Id      AS EnrollmentId,
    e.Number  AS EnrollmentNumber,
    es.Name   AS EnrollmentStatus,
    dt.Name   AS DeviceType,
    f.Name    AS Facility,
    bm.Name   AS BillingMethod,
    bs.Name   AS BillingStatus,
    (SELECT COUNT(*) FROM dbo.PendingAttachment pa WHERE pa.EnrollmentId = e.Id) AS PendingAttachmentCount,
    ord.OrderNumbers,
    ord.OrderCount,
    att.AttachmentIds,
    att.AttachmentCount,
    rep.ReportNumbers,
    rep.ReportTypes,
    rep.ReportCount
FROM dbo.Enrollment e
LEFT JOIN dbo.EnrollmentStatus es ON es.Id = e.StatusId
LEFT JOIN dbo.DeviceType       dt ON dt.Id = e.DeviceTypeId
LEFT JOIN dbo.Facility         f  ON f.Id  = e.FacilityId
LEFT JOIN dbo.BillingMethod    bm ON bm.Id = e.BillingMethodId
LEFT JOIN dbo.BillingStatus    bs ON bs.Id = e.BillingStatusId
OUTER APPLY (
    SELECT STRING_AGG(CAST(o.Number AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY o.Number) AS OrderNumbers,
           COUNT(*) AS OrderCount
    FROM dbo.[Order] o
    WHERE o.EnrollmentId = e.Id
) ord
OUTER APPLY (
    SELECT STRING_AGG(CAST(a.Id AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY a.Id) AS AttachmentIds,
           COUNT(*) AS AttachmentCount
    FROM dbo.Attachment a
    WHERE a.ObjectId = e.Id
) att
OUTER APPLY (
    SELECT STRING_AGG(CAST(r.Number AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY r.Number) AS ReportNumbers,
           STRING_AGG(CAST(rt.Name AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY r.Number) AS ReportTypes,
           COUNT(*) AS ReportCount
    FROM dbo.Report r
    INNER JOIN dbo.ReportType rt ON rt.Id = r.ReportTypeId
    WHERE r.EnrollmentId = e.Id
) rep