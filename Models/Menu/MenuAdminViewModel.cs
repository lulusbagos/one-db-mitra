using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Menu
{
    public class MenuAdminViewModel
    {
        public IReadOnlyList<MenuItem> MenuTree { get; set; } = Array.Empty<MenuItem>();
        public MenuFormModel NewMenu { get; set; } = new();
        public SubMenuFormModel NewSubMenu { get; set; } = new();
        public IEnumerable<SelectListItem> ParentMenuOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public string? Notification { get; set; }
        public string RoleName { get; set; } = "-";
        public int RoleLevel { get; set; }
        public string CompanyName { get; set; } = "-";
        public int ActiveKaryawanCount { get; set; }
        public int ActiveRoleCount { get; set; }
        public int BlacklistCount { get; set; }
        public int ResignCount { get; set; }
        public int RehireCount { get; set; }
        public int ActiveMenuCount { get; set; }
        public int ActiveUserCount { get; set; }
        public int ActiveCompanyCount { get; set; }
        public int ActiveDepartmentCount { get; set; }
        public IReadOnlyList<int> MenuTrend { get; set; } = Array.Empty<int>();
        public IReadOnlyList<int> UserTrend { get; set; } = Array.Empty<int>();
        public IReadOnlyList<int> CompanyTrend { get; set; } = Array.Empty<int>();
        public IReadOnlyList<int> DepartmentTrend { get; set; } = Array.Empty<int>();
        public string StartupPage { get; set; } = "-";
        public HealthStatusViewModel Health { get; set; } = new();
        public IReadOnlyList<CompanyHierarchyItem> CompanyHierarchy { get; set; } = Array.Empty<CompanyHierarchyItem>();
        public IReadOnlyList<AuditLogItem> RecentAudits { get; set; } = Array.Empty<AuditLogItem>();
        public IReadOnlyList<ActivityDayItem> ActivityHeatmap { get; set; } = Array.Empty<ActivityDayItem>();
    }

    public class HealthStatusViewModel
    {
        public bool IsDatabaseUp { get; set; }
        public string JobStatus { get; set; } = "Idle";
        public DateTime? LastSyncAt { get; set; }
        public string LastSyncLabel => LastSyncAt.HasValue ? LastSyncAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm") : "Belum ada";
    }

    public class CompanyHierarchyItem
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public IReadOnlyList<DepartmentHierarchyItem> Departments { get; set; } = Array.Empty<DepartmentHierarchyItem>();
    }

    public class DepartmentHierarchyItem
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public IReadOnlyList<SectionHierarchyItem> Sections { get; set; } = Array.Empty<SectionHierarchyItem>();
    }

    public class SectionHierarchyItem
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public IReadOnlyList<PositionHierarchyItem> Positions { get; set; } = Array.Empty<PositionHierarchyItem>();
    }

    public class PositionHierarchyItem
    {
        public int PositionId { get; set; }
        public string PositionName { get; set; } = string.Empty;
    }

    public class AuditLogItem
    {
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Username { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ActivityDayItem
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class MenuFormModel
    {
        [Required(ErrorMessage = "Nama menu wajib diisi.")]
        [Display(Name = "Nama Menu")]
        [StringLength(80)]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kode menu wajib diisi.")]
        [Display(Name = "Kode Menu")]
        [StringLength(40)]
        public string MenuCode { get; set; } = string.Empty;

        [Display(Name = "Ikon (contoh: bi-speedometer2)")]
        [StringLength(120)]
        public string IconKey { get; set; } = "bi-circle";

        [Display(Name = "Alamat Halaman")]
        [StringLength(250)]
        public string UrlPath { get; set; } = "#";

        [Display(Name = "Buka Tab Baru")]
        public bool OpenInNewTab { get; set; }

        [Display(Name = "Sembunyikan")]
        public bool IsHidden { get; set; }

        [Display(Name = "Urutan")]
        [Range(0, 999)]
        public int SortOrder { get; set; } = 10;

        [Display(Name = "Kategori Startup")]
        [StringLength(50)]
        public string StartupCategory { get; set; } = "general";

        public MenuEditRequest ToRequest()
        {
            return new MenuEditRequest
            {
                DisplayName = DisplayName,
                MenuCode = MenuCode,
                IconKey = IconKey,
                UrlPath = UrlPath,
                OpenInNewTab = OpenInNewTab,
                IsHidden = IsHidden,
                SortOrder = SortOrder,
                StartupCategory = StartupCategory
            };
        }
    }

    public class SubMenuFormModel : MenuFormModel
    {
        [Required(ErrorMessage = "Menu induk wajib dipilih.")]
        [Display(Name = "Menu Induk")]
        public int? ParentMenuId { get; set; }

        public new MenuEditRequest ToRequest()
        {
            if (ParentMenuId is null || ParentMenuId <= 0)
            {
                throw new InvalidOperationException("Menu induk tidak valid.");
            }

            var request = base.ToRequest();
            request.ParentId = ParentMenuId;
            return request;
        }
    }
}
