using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Admin
{
    public class AuditLogIndexViewModel
    {
        public string? EntityFilter { get; set; }
        public string? UsernameFilter { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? RangeFilter { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }
        public IReadOnlyList<AuditLogItem> Logs { get; set; } = Array.Empty<AuditLogItem>();
    }

    public class AuditLogItem
    {
        public int AuditId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string? Key { get; set; }
        public string? Description { get; set; }
        public string? Username { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
