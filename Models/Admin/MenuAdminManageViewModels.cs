using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Admin
{
    public class MenuManageViewModel
    {
        public bool ActiveOnly { get; set; } = true;
        public IReadOnlyList<MenuManageItem> Menus { get; set; } = Array.Empty<MenuManageItem>();
        public IEnumerable<SelectListItem> ParentOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class MenuManageItem
    {
        public int MenuId { get; set; }
        public int? ParentMenuId { get; set; }
        public string MenuCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ParentName { get; set; } = "-";
        public string IconKey { get; set; } = string.Empty;
        public string UrlPath { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsHidden { get; set; }
        public bool OpenInNewTab { get; set; }
        public bool IsActive { get; set; }
    }

    public class MenuEditViewModel
    {
        public int MenuId { get; set; }

        [Required]
        [Display(Name = "Kode Menu")]
        [StringLength(50)]
        public string MenuCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Nama Menu")]
        [StringLength(120)]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "Menu Induk")]
        public int? ParentMenuId { get; set; }

        [Display(Name = "Ikon")]
        [StringLength(100)]
        public string? IconKey { get; set; }

        [Display(Name = "URL")]
        [StringLength(250)]
        [RegularExpression(@"^(#|/).*$", ErrorMessage = "URL harus diawali '/' atau '#'.")]
        public string? UrlPath { get; set; }

        [Display(Name = "Urutan")]
        public int SortOrder { get; set; } = 10;

        [Display(Name = "Sembunyikan")]
        public bool IsHidden { get; set; }

        [Display(Name = "Buka Tab Baru")]
        public bool OpenInNewTab { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<SelectListItem> ParentOptions { get; set; } = Array.Empty<SelectListItem>();
    }
}
