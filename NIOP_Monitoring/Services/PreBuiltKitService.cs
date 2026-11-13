using Dapper;
using NIOP_Monitoring.Models;
using System.Data;

namespace NIOP_Monitoring.Services
{
    public class PreBuiltKitService
    {
        private readonly IDbConnection _connection;

        public PreBuiltKitService(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<PreBuiltKitPathViewModel>> GetPreBuiltKitsLineage()
        {
            const string query = @"select distinct pbk.PackageSerialNumber as PBKSerialNumber, pbks.Name as PBKStatus, c.Name as Center,
                                     b.Number as BundleNumber, pbk.ExpirationDate ExpirationDate,
                                     k.Name as Kit, o.Number as OrderNumber, os.Name as OrderStatus, ot.Name as OrderType,
                                     pbkd.PartNumber as PartNumber
                                     from PreBuiltKit pbk
                                     inner join PreBuiltKitDetail pbkd
                                     on pbk.Id = pbkd.PreBuiltKitId
                                     inner join PreBuiltKitStatus pbks
                                     on pbks.Id = pbk.PreBuiltKitStatusId
                                     inner join Center c
                                     on pbk.CenterId = c.Id
                                     inner join Kit k
                                     on pbk.KitId = k.Id
                                     left join Bundle b
                                     on b.Id = pbk.BundleId
                                     left join [Order] o
                                     on pbk.OrderId = o.Id
                                     left join OrderStatus os
                                     on o.OrderStatusId = os.Id
                                     left join OrderType ot
                                     on o.OrderTypeId = ot.id  
                                     order by PBKSerialNumber";

            var rows = await _connection.QueryAsync<PreBuiltKitPathViewModel, OrderLineagePathViewModel, string, (PreBuiltKitPathViewModel Pbk, string PartNumber)>(query,
            (pbk, order, partNumber) =>
            {
                pbk.Order = order != null && order.OrderNumber > 0 ? order : null;
                return (pbk, partNumber);
            }, splitOn: "OrderNumber,PartNumber");


            return rows
                   .GroupBy(r => r.Pbk.PBKSerialNumber)
                   .Select(g =>
                   {
                       var pbk = g.First().Pbk;
                       pbk.PartNumbers = g.Select(r => r.PartNumber)
                                          .Where(p => !string.IsNullOrEmpty(p))
                                          .Distinct()
                                          .ToList();
                       return pbk;
                   })
                   .ToList();
        }
    }
}
