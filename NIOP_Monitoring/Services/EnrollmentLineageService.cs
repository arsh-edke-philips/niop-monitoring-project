using Dapper;
using NIOP_Monitoring.Models;
using System.Data;

namespace NIOP_Monitoring.Services
{
    public class EnrollmentLineageService
    {
        private readonly IDbConnection _connection;

        public EnrollmentLineageService(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<EnrollmentPathViewModel>> GetEnrollmentLineage(CancellationToken ct)
        {
            const string query = @"SELECT
                                  e.Number AS EnrollmentNumber,
                                  es.Name AS EnrollmentStatus,
                                  dt.Name AS DeviceType,
                                  f.Name as Facility,
                                  bm.Name AS BillingMethod,
                                  bs.Name AS BillingStatus,
                                  (SELECT COUNT(*) FROM PendingAttachment pa WHERE pa.EnrollmentId = e.Id) AS PendingAttachmentCount,
                                  o.Number AS OrderNumber,        
                                  a.Id as AttachmentId,
                                  a.ContentType AS ContentType,
                                  at.Name AS AttachmentType,
                                  r.Number AS ReportNumber,
                                  rt.Name AS ReportType
                                FROM Enrollment e
                                INNER JOIN EnrollmentStatus es ON es.Id = e.StatusId
                                INNER JOIN DeviceType dt ON dt.Id = e.DeviceTypeId
                                INNER JOIN Facility f on f.Id = e.FacilityId
                                INNER JOIN BillingMethod bm ON bm.Id = e.BillingMethodId
                                LEFT JOIN [Novus].[dbo].[Order] o on o.EnrollmentId = e.Id
                                LEFT JOIN BillingStatus bs ON bs.Id = e.BillingStatusId
                                LEFT JOIN Attachment a ON a.ObjectId = e.Id
                                LEFT JOIN AttachmentType at ON at.Id = a.AttachmentTypeId
                                LEFT JOIN Report r ON r.EnrollmentId = e.Id
                                LEFT JOIN ReportType rt ON rt.Id = r.ReportTypeId
                                ORDER BY e.Number";

            var rows = await _connection.QueryAsync<EnrollmentPathViewModel, AttachmentViewModel, ReportPathViewModel, EnrollmentPathViewModel>(
                    new CommandDefinition(query, cancellationToken: ct),
                    (enrollment, attachment, report) =>
                    {
                        enrollment.Report = report;
                        if (attachment != null)
                        {
                            enrollment.Attachment.Add(attachment);
                        }
                        return enrollment;
                    },
                    splitOn: "AttachmentId, ReportNumber"
            );

            var result = rows
                .GroupBy(x => x.EnrollmentNumber)
                .Select(g =>
                {
                    var enrollment = g.First();
                    enrollment.Attachment = g.SelectMany(x => x.Attachment).ToList();
                    return enrollment;
                })
                .ToList();

            return result;
        }
    }
}
