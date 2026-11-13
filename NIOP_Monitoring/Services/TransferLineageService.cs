using Dapper;
using NIOP_Monitoring.Models;
using System.Data;
using System.Diagnostics.Contracts;

namespace NIOP_Monitoring.Services
{
    public class TransferLineageService
    {
        private readonly IDbConnection _connection;

        public TransferLineageService(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<TransferPathViewModel>> GetTransferLineage()
        {
            const string query = @"select t.Number as TransferNumber, ts.Name as TransferStatus, n.Value as Note, fc.Name as FromCenter, tc.Name as ToCenter,
                                     d.SerialNumber as Device, dt.Name as DeviceType from Transfer t
                                     inner join TransferStatus ts
                                     on ts.Id = t.TransferStatusId
                                     inner join Center fc
                                     on t.FromCenterId = fc.Id
                                     inner join Center tc
                                     on t.ToCenterId = tc.Id
                                     inner join TransferDetail td
                                     on td.TransferId = t.Id
                                     inner join Device d
                                     on td.DeviceId = d.Id
                                     inner join DeviceType dt
                                     on d.DeviceTypeId = dt.Id
                                     left join Note n
                                     on n.Id = t.NoteId
                                     order by t.Number";

            var rows = await _connection.QueryAsync<TransferPathViewModel, DeviceLineagePathViewModel, TransferPathViewModel>(query, (transfer, device) =>
            {
                transfer.Device.Add(device);
                return transfer;
            }, splitOn: "Device");

            var result = rows.GroupBy(t => t.TransferNumber).Select(g =>
            {
                var transfer = g.First();
                transfer.Device = g.SelectMany(t => t.Device).ToList();
                return transfer;
            }).ToList();

            return result;
        }
    }
}
