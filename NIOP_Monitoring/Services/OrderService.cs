using Dapper;
using NIOP_Monitoring.Models;
using System.Data;

namespace NIOP_Monitoring.Services
{
    public class OrderService
    {
        private readonly IDbConnection _connection;
        private readonly OrderSummaryService _service;

        public OrderService(IDbConnection connection, OrderSummaryService service)
        {
            _service = service;
            _connection = connection;
        }

        public async Task<OrderMetricsViewModel> GetOrderService()
        {
            var orders = new OrderMetricsViewModel
            {
                PendingOrders = await _service.GetOrderCountByStatus("Pending"),
                PendingByDistributionCenter = await GetPendingOrderCountByDistributionCenter(),
                ShippedWithinDeliveryDatePercentage = await _service.GetOnTimeShippingPercentage(),
                AvgFulfillmentDurationDays = await GetAvgFulfillmentDurationDays(),
                DelayedOrders = await GetDelayedOrders(),
                CancelledOrderVolume = await GetCancelledOrderVolume(),
                //ParLevelAlerts = await GetParLevelAlerts()
            };

            return orders;
        }

        private async Task<List<DistributionCenterPendingViewModel>> GetPendingOrderCountByDistributionCenter()
        {
            const string query = @"
                SELECT c.Name as DistributionCenter, count(*) as PendingOrders
                FROM [Novus].[dbo].[Order] as o
                INNER JOIN [Novus].[dbo].[Center] as c
                ON o.CenterId = c.Id
                INNER JOIN [Novus].[dbo].[OrderStatus] as os
                ON o.OrderStatusId = os.Id
                WHERE os.Name = 'Pending'   
                GROUP BY c.Name";

            //Console.WriteLine("pending by center: " + string.Join(", ", (await _connection.QueryAsync<DistributionCenterPendingViewModel>(query)).ToList()));

            return (await _connection.QueryAsync<DistributionCenterPendingViewModel>(query)).ToList();
        }

        private async Task<decimal> GetAvgFulfillmentDurationDays()
        {
            const string query = @"SELECT 
                                      CAST(
                                          AVG(DATEDIFF(DAY, o.CreatedDate, o.FulfilledDate) * 1.0)
                                          AS DECIMAL(10, 2)
                                      ) AS AverageFullfillmentDays
                                      FROM [Novus].[dbo].[Order] AS o
                                      INNER JOIN  [Novus].[dbo].[OrderStatus] AS os
                                      ON os.Id = o.OrderStatusId
                                      WHERE os.Name = 'Completed'
                                          AND o.FulfilledDate IS NOT NULL;";
            return await _connection.ExecuteScalarAsync<decimal>(query);
        }

        private async Task<int> GetDelayedOrders()
        {
            const string query = @"
                                SELECT COUNT(*) FROM [Novus].[dbo].[Order] AS o
                                INNER JOIN [Novus].[dbo].[OrderStatus] AS os
                                ON os.Id = o.OrderStatusId
                                WHERE o.DeliverByDate < CAST(GETDATE() AS DATE)
                                AND os.Name = 'Pending'";

            return await _connection.ExecuteScalarAsync<int>(query);
        }

        private async Task<int> GetCancelledOrderVolume()
        {
            const string query = @"
                                SELECT COUNT(*) FROM [Novus].[dbo].[Order] AS o
                                INNER JOIN [Novus].[dbo].[OrderStatus] AS os
                                ON os.Id = o.OrderStatusId
                                WHERE os.Name = 'Cancelled'";

   
            return await _connection.ExecuteScalarAsync<int>(query);
        }
    }
}
