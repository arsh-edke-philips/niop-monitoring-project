using NIOP_Monitoring.Models;
using Dapper;
using System.Data;
using System.Globalization;


namespace NIOP_Monitoring.Services
{
    public class OrderSummaryService
    {
        private readonly IDbConnection _dbConnection;

        public OrderSummaryService(IDbConnection connection)
        {
            _dbConnection = connection;
        }
        public async Task<SummaryMetricsViewModel> GetOrderSummary()
        {
            SummaryMetricsViewModel _summary = new SummaryMetricsViewModel();
            _summary.PendingOrders = await GetOrderCountByStatus("Pending");
            _summary.OrdersCreatedToday = await GetOrderCountByPeriod("Daily");
            _summary.OrdersCreatedThisWeek = await GetOrderCountByPeriod("Weekly");
            _summary.OrdersCreatedThisMonth = await GetOrderCountByPeriod("Monthly");
            _summary.ShippedWithinDeliveryDatePercentage = await GetOnTimeShippingPercentage();
            return _summary;
        }

        public async Task<int> GetOrderCountByStatus(string status)
        {
            var query = @"
               SELECT COUNT(*) 
                FROM [Novus].[dbo].[Order] as o
                INNER JOIN [Novus].[dbo].[OrderStatus] as os ON o.OrderStatusId = os.Id
                WHERE os.Name = @Status";

            var parameters = new { Status = status };
            return await _dbConnection.ExecuteScalarAsync<int>(query, parameters);
        }

        public async Task<int> GetOrderCountByPeriod(string period)
        {
            var query = @"
                SELECT COUNT(*) 
                FROM[Novus].[dbo].[Order] as o
                WHERE o.CreatedDate >= DATEADD(DAY, -@Days, GETDATE())"
               ;
            int days = period switch
            {
                "Daily" => 1,
                "Weekly" => 7,
                "Monthly" => 30,
                _ => throw new ArgumentException("Invalid period specified.")
            };
            var parameters = new { Days = days };
            return await _dbConnection.ExecuteScalarAsync<int>(query, parameters);
        }

        public async Task<decimal> GetOnTimeShippingPercentage()
        {
            var query = @"
                SELECT 
                CAST(
                    COUNT(CASE WHEN o.FulfilledDate <= o.DeliverByDate THEN 1 END) * 100.0 / COUNT(*) AS DECIMAL(5, 2) ) as OnTimeShippingPercentage
                    FROM [Novus].[dbo].[Order] as o
                    INNER JOIN [Novus].[dbo].[OrderStatus] as os ON o.OrderStatusId = os.Id
                    WHERE os.Name = 'Completed'
               ";
            return await _dbConnection.ExecuteScalarAsync<decimal>(query);
        }
    }
}

