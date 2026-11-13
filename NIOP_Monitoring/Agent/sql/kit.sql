SELECT
    k.Name AS Kit,
    (SELECT COUNT(*) FROM dbo.Bundle b      WHERE b.KitId = k.Id) AS BundleCount,
    (SELECT COUNT(*) FROM dbo.PreBuiltKit p WHERE p.KitId = k.Id) AS PBKCount
FROM dbo.Kit k