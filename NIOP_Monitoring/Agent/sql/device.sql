SELECT
    d.SerialNumber,
    d.PartNumber,
    lo.BundleId,
    lo.OrderNumber,
    ords.OrderNumbers,
    ords.OrderCount,
    pbk.PackageSerialNumber   AS PBKSerialNumber,
    l.Name               AS Location,
    c.Name               AS Center,
    dt.Name              AS DeviceType,
    RTRIM(dc.Name)       AS DeviceCategory,
    dcfg.Name            AS DeviceConfig,
    f.Name               AS Facility,
    lt.TransferNumber,
    d.PurchaseOrderNumber
FROM dbo.Device d
LEFT JOIN PreBuiltKitDetail  pbkd ON pbkd.DeviceId = d.Id
LEFT JOIN PreBuiltKit        pbk  ON pbk.Id = pbkd.PreBuiltKitId
LEFT JOIN dbo.DeviceType     dt   ON dt.Id   = d.DeviceTypeId
LEFT JOIN dbo.DeviceCategory dc   ON dc.Id   = dt.DeviceCategoryId
LEFT JOIN dbo.DeviceConfig   dcfg ON dcfg.Id = d.DeviceConfigId
LEFT JOIN dbo.Location       l    ON l.Id    = d.LocationId
LEFT JOIN dbo.Center         c    ON c.Id    = d.CenterId
LEFT JOIN dbo.Facility       f    ON f.Id    = d.FacilityId
OUTER APPLY (
    SELECT TOP (1) od.BundleId, o.Number AS OrderNumber
    FROM dbo.OrderDetail od
    JOIN dbo.[Order] o ON o.Id = od.OrderId
    WHERE od.DeviceId = d.Id
    ORDER BY od.Id DESC
) lo
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.Number AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.Number) AS OrderNumbers,
           COUNT(*) AS OrderCount
    FROM (SELECT DISTINCT o.Number
          FROM dbo.OrderDetail od
          JOIN dbo.[Order] o ON o.Id = od.OrderId
          WHERE od.DeviceId = d.Id) x
) ords
OUTER APPLY (
    SELECT TOP (1) t.Number AS TransferNumber
    FROM dbo.TransferDetail td
    JOIN dbo.Transfer t ON t.Id = td.TransferId
    WHERE td.DeviceId = d.Id
    ORDER BY td.Id DESC
) lt
WHERE d.Id = (SELECT MAX(d2.Id)
              FROM dbo.Device d2
              WHERE d2.SerialNumber = d.SerialNumber)
