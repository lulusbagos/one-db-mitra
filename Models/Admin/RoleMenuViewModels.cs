using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Admin
{
    public class RoleMenuViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public IEnumerable<SelectListItem> RoleOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<MenuAccessItem> Menus { get; set; } = Array.Empty<MenuAccessItem>();
        public HashSet<int> SelectedMenuIds { get; set; } = new();
    }

    public class CompanyMenuViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<MenuAccessItem> Menus { get; set; } = Array.Empty<MenuAccessItem>();
        public HashSet<int> SelectedMenuIds { get; set; } = new();
    }

    public class MenuAccessMatrixViewModel
    {
        public int RoleId { get; set; }
        public int CompanyId { get; set; }
        public IEnumerable<SelectListItem> RoleOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<MenuAccessItem> Menus { get; set; } = Array.Empty<MenuAccessItem>();
        public HashSet<int> RoleMenuIds { get; set; } = new();
        public HashSet<int> CompanyMenuIds { get; set; } = new();
    }

    public class MenuAccessItem
    {
        public int MenuId { get; set; }
        public int? ParentMenuId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string MenuCode { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int Depth { get; set; }
    }
}
