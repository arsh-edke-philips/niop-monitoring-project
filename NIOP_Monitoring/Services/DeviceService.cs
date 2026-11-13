using Dapper;
using NIOP_Monitoring.Models;
using System.Data;

namespace NIOP_Monitoring.Services
{
    public class DeviceService
    {
        private readonly IDbConnection _connection;

        public DeviceService(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<DeviceMetricsViewModel> GetDeviceMetrics()
        {
            const string query = @"WITH received AS (
                                SELECT
                                    dl.DeviceId,
                                    MIN(dl.CreatedDate) AS ReceivedDate
                                FROM DeviceLog dl
                                WHERE dl.Description LIKE 'Device with Serial number % was added %'
                                GROUP BY dl.DeviceId
                                ),
                                final_stage AS (
                                    SELECT
                                        dl.DeviceId,
                                        MIN(dl.CreatedDate) AS FinalStageDate
                                    FROM DeviceLog dl
                                    WHERE
                                        dl.Description LIKE 'Device % placed in Pre-Built Kit %'
                                        OR (
                                            dl.Description LIKE 'Device % Ready To Ship'
                                            AND NOT EXISTS (
                                                SELECT 1
                                                FROM DeviceLog d
                                                WHERE d.DeviceId = dl.DeviceId
                                                  AND d.Description LIKE '% Kitted %'
                                                  AND d.CreatedDate > dl.CreatedDate
                                            )
                                        )
                                    GROUP BY dl.DeviceId
                                )
                                SELECT
                                    AVG(CAST(DATEDIFF(DAY, r.ReceivedDate, f.FinalStageDate) AS float)) AS AvgVendorToTestedAndShippedDays
                                FROM received r
                                JOIN final_stage f
                                    ON f.DeviceId = r.DeviceId
                                WHERE f.FinalStageDate > r.ReceivedDate";

            var result = _connection.QuerySingleAsync<DeviceMetricsViewModel>(query).Result;
            return result;
        }
    }
}
