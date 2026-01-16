using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Admin
{
    public class UserAdminIndexViewModel
    {
        public bool ActiveOnly { get; set; } = true;
        public IReadOnlyList<UserAdminListItem> Users { get; set; } = Array.Empty<UserAdminListItem>();
        public IReadOnlyList<RoleListItem> Roles { get; set; } = Array.Empty<RoleListItem>();
    }

    public class UserAdminListItem
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int PrimaryRoleId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsOnline { get; set; }
    }

    public class UserEditViewModel
    {
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [Display(Name = "Nama Lengkap")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(150)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Perusahaan")]
        public int CompanyId { get; set; }

        [Display(Name = "Role")]
        public int RoleId { get; set; }

        [Display(Name = "Role")]
        public List<int> RoleIds { get; set; } = new();

        [Display(Name = "Departemen")]
        public int? DepartmentId { get; set; }

        [Display(Name = "Section")]
        public int? SectionId { get; set; }

        [Display(Name = "Jabatan")]
        public int? PositionId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> RoleOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DepartmentOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> SectionOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> PositionOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class RoleListItem
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int AccessLevel { get; set; }
        public bool IsActive { get; set; }
        public string StartupUrl { get; set; } = string.Empty;
    }
}
