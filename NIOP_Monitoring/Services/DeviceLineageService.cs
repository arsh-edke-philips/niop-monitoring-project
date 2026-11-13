using Dapper;
using NIOP_Monitoring.Models;
using System.Data;

namespace NIOP_Monitoring.Services
{
    public class DeviceLineageService
    {
        private readonly IDbConnection _connection;

        public DeviceLineageService (IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<DeviceLineagePathViewModel>> GetDeviceLineage()
        {
            const string query = @" select d.SerialNumber as Device, d.PartNumber as Part, od.BundleId as Bundle, od.OrderId as [Order], l.Name as Location,
                                      c.Name as Center, dt.Name as DeviceType, dc.Name as DeviceCategory, dcfg.Name as deviceConfig, f.Name as Facility,
                                      d.PurchaseOrderNumber as PurchaseOrderNumber from Device d
                                      inner join DeviceType dt
                                      on dt.Id = d.DeviceTypeId
                                      inner join DeviceCategory dc
                                      on dt.DeviceCategoryId = dc.Id
                                      inner join DeviceConfig dcfg
                                      on d.DeviceConfigId = dcfg.Id
                                      inner join Location l
                                      on l.Id = d.LocationId
                                      inner join Center c
                                      on c.Id = d.CenterId
                                      left join Facility f
                                      on f.Id = d.FacilityId
                                      left join OrderDetail od
                                      on d.Id = od.DeviceId";

            var result = await _connection.QueryAsync<DeviceLineagePathViewModel>(query);
            return result.ToList();
        }
    }
}
