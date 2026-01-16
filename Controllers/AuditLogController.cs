using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;

namespace one_db_mitra.Controllers
{
    [Authorize]
    public class AuditLogController : Controller
    {
        private readonly OneDbMitraContext _context;

        public AuditLogController(OneDbMitraContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? entity, string? username, DateTime? from, DateTime? to, string? range, int page = 1, CancellationToken cancellationToken = default)
        {
            var query = _context.tbl_r_audit_log.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(entity))
            {
                query = query.Where(log => log.entitas == entity);
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                query = query.Where(log => log.username != null && log.username.Contains(username));
            }

            if (!string.IsNullOrWhiteSpace(range))
            {
                var today = DateTime.Today;
                if (string.Equals(range, "today", StringComparison.OrdinalIgnoreCase))
                {
                    from = today;
                    to = today;
                }
                else if (string.Equals(range, "7days", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddDays(-6);
                    to = today;
                }
            }

            if (from.HasValue)
            {
                query = query.Where(log => log.dibuat_pada >= from.Value);
            }

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(log => log.dibuat_pada < end);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var pageSize = 50;
            var currentPage = page < 1 ? 1 : page;
            var skip = (currentPage - 1) * pageSize;

            var logs = await query
                .OrderByDescending(log => log.audit_id)
                .Skip(skip)
                .Take(pageSize)
                .Select(log => new AuditLogItem
                {
                    AuditId = log.audit_id,
                    Action = log.aksi,
                    Entity = log.entitas,
                    Key = log.kunci,
                    Description = log.deskripsi,
                    Username = log.username,
                    CreatedAt = log.dibuat_pada
                })
                .ToListAsync(cancellationToken);

            var model = new AuditLogIndexViewModel
            {
                EntityFilter = entity,
                UsernameFilter = username,
                FromDate = from,
                ToDate = to,
                RangeFilter = range,
                Page = currentPage,
                PageSize = pageSize,
                TotalCount = totalCount,
                Logs = logs
            };

            return View(model);
        }
    }
}
