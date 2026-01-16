using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using one_db_mitra.Data;
using one_db_mitra.Models.Db;
using one_db_mitra.Hubs;

namespace one_db_mitra.Services.Audit
{
    public class AuditLogger
    {
        private readonly OneDbMitraContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHubContext<NotificationsHub> _hubContext;

        public AuditLogger(OneDbMitraContext context, IHttpContextAccessor httpContextAccessor, IHubContext<NotificationsHub> hubContext)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _hubContext = hubContext;
        }

        public async Task LogAsync(string action, string entity, string? key, string? description, CancellationToken cancellationToken = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userIdValue = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = httpContext?.User?.Identity?.Name;

            _context.tbl_r_audit_log.Add(new tbl_r_audit_log
            {
                aksi = action,
                entitas = entity,
                kunci = key,
                deskripsi = description,
                user_id = int.TryParse(userIdValue, out var parsed) ? parsed : null,
                username = string.IsNullOrWhiteSpace(userName) ? null : userName,
                dibuat_pada = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            var toastType = action.Equals("DELETE", StringComparison.OrdinalIgnoreCase)
                ? "danger"
                : action.Equals("UPDATE", StringComparison.OrdinalIgnoreCase)
                    ? "warning"
                    : "success";
            var message = string.IsNullOrWhiteSpace(description)
                ? $"{action} {entity}"
                : description;

            await _hubContext.Clients.All.SendAsync("notify", new
            {
                message,
                type = toastType
            }, cancellationToken);
        }
    }
}
