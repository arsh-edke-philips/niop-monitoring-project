SELECT
    od.Id          AS OrderDeviceId,
    o.Number       AS OrderNumber,
    os.Name        AS OrderStatus,
    ot.Name        AS OrderType,
    c.Name         AS Center,
    d.SerialNumber AS SerialNumber,
    dt.Name        AS DeviceType,
    od.BundleId    AS BundleId,
    b.Number       AS BundleNumber
FROM dbo.OrderDetail od
JOIN      dbo.[Order]     o  ON o.Id  = od.OrderId
JOIN      dbo.Device      d  ON d.Id  = od.DeviceId
LEFT JOIN dbo.OrderStatus os ON os.Id = o.OrderStatusId
LEFT JOIN dbo.OrderType   ot ON ot.Id = o.OrderTypeId
LEFT JOIN dbo.Center      c  ON c.Id  = o.CenterId
LEFT JOIN dbo.DeviceType  dt ON dt.Id = d.DeviceTypeId
LEFT JOIN dbo.Bundle      b  ON b.Id  = od.BundleId