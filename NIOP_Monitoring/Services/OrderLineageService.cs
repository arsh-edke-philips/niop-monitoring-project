using Dapper;
using NIOP_Monitoring.Models;
using System.Data;

namespace NIOP_Monitoring.Services
{
    public class OrderLineageService
    {
        private readonly IDbConnection _dbConnection;

        public OrderLineageService(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<List<OrderLineagePathViewModel>> GetOrderLineagePath()
        {
            const string query = @"select distinct o.Number as OrderNumber, od.BundleId as Bundle, k.Name as Kit, pbk.Id as PreBuiltKit,
                                      od.DeviceId as Device, f.Name as Facility,
                                      ot.Name as OrderType, os.Name as OrderStatus, c.Name as Center, o.CreatedUser as CreatedUser
                                      from [Novus].[dbo].[Order] o
                                      inner join OrderDetail as od
                                      on o.Id = od.OrderId
                                      left join Bundle b
                                      on b.Id = od.BundleId
                                      left join Kit k
                                      on k.Id = b.KitId
                                      inner join OrderType as ot
                                      on ot.Id = o.OrderTypeId
                                      inner join OrderStatus as os
                                      on os.Id = o.OrderStatusId
                                      inner join Center as c
                                      on c.Id = o.CenterId
                                      Left join Facility as f
                                      on f.Id = o.FacilityId
                                      left join PreBuiltKit as pbk
                                      on o.Id = pbk.OrderId";

            var result = await _dbConnection.QueryAsync<OrderLineagePathViewModel>(query);
            return result.ToList();
        }
    }
}
