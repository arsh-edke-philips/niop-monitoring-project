SELECT
    a.Id          AS AttachmentId,
    at.Name       AS AttachmentType,
    ot.Name       AS ObjectType,
    a.ContentType,
    a.ObjectId,
    e.Number      AS EnrollmentNumber,
    e.Id          AS EnrollmentId
FROM dbo.Attachment a
LEFT JOIN dbo.AttachmentType at ON at.Id = a.AttachmentTypeId
LEFT JOIN dbo.ObjectType     ot ON ot.Id = a.ObjectTypeId
LEFT JOIN dbo.Enrollment     e  ON e.Id  = a.ObjectId AND ot.Name = 'Enrollment'