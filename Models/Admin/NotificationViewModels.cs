using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Admin
{
    public class NotificationItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
        public string Category { get; set; } = "general";
        public DateTime CreatedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public string? Link { get; set; }
    }

    public class NotificationIndexViewModel
    {
        public IReadOnlyList<NotificationItem> Items { get; set; } = Array.Empty<NotificationItem>();
    }
}
