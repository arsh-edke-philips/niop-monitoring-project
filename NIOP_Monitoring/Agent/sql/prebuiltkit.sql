SELECT
    pbk.PackageSerialNumber AS PBKSerialNumber,
    pbks.Name               AS PBKStatus,
    c.Name                  AS Center,
    pbk.BundleId,
    b.Number                AS BundleNumber,
    pbk.ExpirationDate,
    k.Name                  AS Kit,
    o.Number                AS OrderNumber,
    os.Name                 AS OrderStatus,
    ot.Name                 AS OrderType,
    dev.DeviceSerials,
    dev.DeviceCount,
    prt.PartNumbers,
    prt.PartCount
FROM dbo.PreBuiltKit pbk
LEFT JOIN dbo.PreBuiltKitStatus pbks ON pbks.Id = pbk.PreBuiltKitStatusId
LEFT JOIN dbo.Center            c    ON c.Id    = pbk.CenterId
LEFT JOIN dbo.Kit               k    ON k.Id    = pbk.KitId
LEFT JOIN dbo.Bundle            b    ON b.Id    = pbk.BundleId
LEFT JOIN dbo.[Order]           o    ON o.Id    = pbk.OrderId
LEFT JOIN dbo.OrderStatus       os   ON os.Id   = o.OrderStatusId
LEFT JOIN dbo.OrderType         ot   ON ot.Id   = o.OrderTypeId
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.SerialNumber AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.SerialNumber) AS DeviceSerials,
           COUNT(*) AS DeviceCount
    FROM (SELECT DISTINCT d.SerialNumber
          FROM dbo.PreBuiltKitDetail pd
          JOIN dbo.Device d ON d.Id = pd.DeviceId
          WHERE pd.PreBuiltKitId = pbk.Id) x
) dev
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.PartNumber AS NVARCHAR(MAX)), ', ')
               WITHIN GROUP (ORDER BY x.PartNumber) AS PartNumbers,
           COUNT(*) AS PartCount
    FROM (SELECT DISTINCT pd.PartNumber
          FROM dbo.PreBuiltKitDetail pd
          WHERE pd.PreBuiltKitId = pbk.Id AND pd.PartNumber IS NOT NULL) x
) prt