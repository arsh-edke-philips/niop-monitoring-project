SELECT
    t.Number  AS TransferNumber,
    ts.Name   AS TransferStatus,
    n.Value   AS Note,
    fc.Name   AS FromCenter,
    tc.Name   AS ToCenter,
    dev.DeviceSerials,
    dev.DeviceCount,
    dty.DeviceTypes
FROM dbo.Transfer t
LEFT JOIN dbo.TransferStatus ts ON ts.Id = t.TransferStatusId
LEFT JOIN dbo.Center         fc ON fc.Id = t.FromCenterId
LEFT JOIN dbo.Center         tc ON tc.Id = t.ToCenterId
LEFT JOIN dbo.Note           n  ON n.Id  = t.NoteId
OUTER APPLY (
    SELECT STRING_AGG(CAST(d.SerialNumber AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY d.SerialNumber) AS DeviceSerials,
           COUNT(*) AS DeviceCount
    FROM dbo.TransferDetail td
    JOIN dbo.Device d ON d.Id = td.DeviceId
    WHERE td.TransferId = t.Id
) dev
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.TypeName AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.TypeName) AS DeviceTypes
    FROM (SELECT DISTINCT dt.Name AS TypeName
          FROM dbo.TransferDetail td
          JOIN dbo.Device d      ON d.Id  = td.DeviceId
          JOIN dbo.DeviceType dt ON dt.Id = d.DeviceTypeId
          WHERE td.TransferId = t.Id) x
) dty