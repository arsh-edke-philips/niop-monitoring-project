using NIOP_Monitoring.Models;
using System.Data;
using Dapper;

namespace NIOP_Monitoring.Services
{
    public class DistributionCenterDeviceService
    {
        private readonly IDbConnection _connection;

        public DistributionCenterDeviceService(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<CenterDeviceAddedCount>> GetDeviceCountAsync()
        {
            const string query = @"SELECT 
                                    c.Id,
                                    c.Name as CenterName,
                                    COUNT(CASE WHEN CAST(dl.CreatedDate AS DATE) = CAST(GETDATE() AS DATE) THEN 1 END) AS TodayCount,
                                    COUNT(CASE WHEN dl.CreatedDate >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE)) 
                                               AND CAST(dl.CreatedDate AS DATE) < CAST(GETDATE() AS DATE) THEN 1 END) AS LastWeekCount,
                                    COUNT(CASE WHEN dl.CreatedDate >= DATEADD(MONTH, -1, CAST(GETDATE() AS DATE)) 
                                               AND CAST(dl.CreatedDate AS DATE) < DATEADD(DAY, -7, CAST(GETDATE() AS DATE)) THEN 1 END) AS LastMonthCount,
                                    COUNT(*) AS TotalCount
                                    FROM DeviceLog AS dl
                                    inner join Device as d
                                    on d.Id = dl.DeviceId
                                    inner join Center as c
                                    on c.Id = d.CenterId
                                    where dl.Description like 'Device with Serial number % was added %'
                                    group by c.Id, c.Name";

            var result = await _connection.QueryAsync<CenterDeviceAddedCount>(query);
            return result.ToList();
        }

        public async Task<List<LocationDeviceCountPerCenter>> GetLocationDeviceCountPerCenterAsync()
        {
            const string query = @"select 
                            c.Id as CenterId,
                            c.Name as CenterName,
                            count(d.Id) as DeviceCount,
                            l.Name as LocationName
                            from Device as d
                            inner join Location as l
                            on l.Id = d.LocationId
                            inner join Center as c
                            on d.CenterId = c.Id
                            group by c.Id, c.Name, l.Name";

            var rows = await _connection.QueryAsync<(Guid CenterId, string CenterName, int Count, string LocationName)>(query);

            var result = rows
                .GroupBy(r => new { r.CenterId, r.CenterName })
                .Select(g => new LocationDeviceCountPerCenter
                {
                    CenterId = g.Key.CenterId,
                    CenterName = g.Key.CenterName,
                    LocationCounts = g.Select(r => new LocationDeviceCount
                    {
                        Location = r.LocationName,
                        Count = r.Count,
                    }).ToList()
                }).ToList();

            return result;
        }
    }
}
