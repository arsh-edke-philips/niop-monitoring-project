SELECT
    o.Number   AS OrderNumber,
    ot.Name    AS OrderType,
    os.Name    AS OrderStatus,
    c.Name     AS Center,
    f.Name     AS Facility,
    e.Id       AS EnrollmentId,
    e.Number   AS EnrollmentNumber,
    o.CreatedUser,
    dev.DeviceSerials,
    dev.DeviceCount,
    bun.BundleIds,
    bun.BundleCount,
    kit.Kits,
    pbk.PBKSerialNumbers,
    pbk.PBKCount
FROM dbo.[Order] o
LEFT JOIN dbo.OrderType   ot ON ot.Id = o.OrderTypeId
LEFT JOIN dbo.OrderStatus os ON os.Id = o.OrderStatusId
LEFT JOIN dbo.Center      c  ON c.Id  = o.CenterId
LEFT JOIN dbo.Facility    f  ON f.Id  = o.FacilityId
LEFT JOIN dbo.Enrollment  e  ON e.Id = o.EnrollmentId
OUTER APPLY (
    SELECT STRING_AGG(CAST(d.SerialNumber AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY d.SerialNumber) AS DeviceSerials,
           COUNT(*) AS DeviceCount
    FROM dbo.OrderDetail od
    JOIN dbo.Device d ON d.Id = od.DeviceId
    WHERE od.OrderId = o.Id
) dev
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.BundleId AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.BundleId) AS BundleIds,
           COUNT(*) AS BundleCount
    FROM (SELECT DISTINCT od.BundleId
          FROM dbo.OrderDetail od
          WHERE od.OrderId = o.Id AND od.BundleId IS NOT NULL) x
) bun
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.KitName AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.KitName) AS Kits
    FROM (SELECT DISTINCT k.Name AS KitName
          FROM dbo.OrderDetail od
          JOIN dbo.Bundle b ON b.Id = od.BundleId
          JOIN dbo.Kit    k ON k.Id = b.KitId
          WHERE od.OrderId = o.Id) x
) kit
OUTER APPLY (
    SELECT STRING_AGG(CAST(p.PackageSerialNumber AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY p.PackageSerialNumber) AS PBKSerialNumbers,
           COUNT(*) AS PBKCount
    FROM dbo.PreBuiltKit p
    WHERE p.OrderId = o.Id
) pbk