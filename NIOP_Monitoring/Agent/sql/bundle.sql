SELECT
    b.Id     AS BundleId,
    b.Number AS BundleNumber,
    k.Name   AS Kit,
    dev.DeviceSerials,
    dev.DeviceCount,
    ord.OrderNumbers,
    pbk.PBKSerialNumbers
FROM dbo.Bundle b
LEFT JOIN dbo.Kit k ON k.Id = b.KitId
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.SerialNumber AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.SerialNumber) AS DeviceSerials,
           COUNT(*) AS DeviceCount
    FROM (SELECT DISTINCT d.SerialNumber
          FROM dbo.OrderDetail od
          JOIN dbo.Device d ON d.Id = od.DeviceId
          WHERE od.BundleId = b.Id) x
) dev
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.Number AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.Number) AS OrderNumbers
    FROM (SELECT DISTINCT o.Number
          FROM dbo.OrderDetail od
          JOIN dbo.[Order] o ON o.Id = od.OrderId
          WHERE od.BundleId = b.Id) x
) ord
OUTER APPLY (
    SELECT STRING_AGG(CAST(p.PackageSerialNumber AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY p.PackageSerialNumber) AS PBKSerialNumbers
    FROM dbo.PreBuiltKit p
    WHERE p.BundleId = b.Id
) pbk