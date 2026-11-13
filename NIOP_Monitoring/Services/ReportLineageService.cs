using Dapper;
using NIOP_Monitoring.Models;
using System.Data;

namespace NIOP_Monitoring.Services
{
    public class ReportLineageService
    {
        private readonly IDbConnection _connection;

        public ReportLineageService(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<ReportPathViewModel>> GetReportLineage()
        {
            const string query = @"
                                select r.Number as ReportNumber, rs.Name as ReportStatus, rt.Name as ReportType,
                                r.OnHold as OnHold, r.OnHoldBy as OnHoldBy, OnHoldReason as OnHoldReason,
                                a.Id as AttachmentId, at.Name as AttachmentType, ot.Name as ObjectType, a.ContentType as ContentType,
                                e.Number as EnrollmentNumber, es.Name as EnrollmentStatus, dt.Name as DeviceType, f.Name as Facility
                                from Report r
                                inner join ReportStatus rs
                                on rs.Id = r.ReportStatusId
                                inner join ReportType rt
                                on rt.Id = r.ReportTypeId
                                inner join Facility f
                                on f.Id = r.FacilityId
                                inner join Enrollment e
                                on e.Id = r.EnrollmentId
                                inner join EnrollmentStatus es
                                on es.Id = e.StatusId
                                inner join DeviceType dt
                                on e.DeviceTypeId = dt.Id
                                left join Attachment a
                                on a.Id = r.ReportFileId
                                left join AttachmentType at
                                on a.AttachmentTypeId = at.Id
                                left join ObjectType ot
                                on a.ObjectTypeId = ot.Id";

            var result = await _connection.QueryAsync<ReportPathViewModel, AttachmentViewModel, EnrollmentPathViewModel, ReportPathViewModel>(
                query,
                (report, attachment, enrollment) =>
                {
                    report.Attachment = attachment;
                    report.Enrollment = enrollment;
                    return report;
                },
                splitOn: "AttachmentId, EnrollmentNumber"
            );
            return result.ToList();
        }
    }
}
