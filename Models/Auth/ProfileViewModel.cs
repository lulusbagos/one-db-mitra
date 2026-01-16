using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace one_db_mitra.Models.Auth
{
    public class ProfileViewModel
    {
        public int UserId { get; set; }

        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Nama Lengkap")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Foto Profil (URL)")]
        [StringLength(500)]
        public string? PhotoUrl { get; set; }

        public string? PhotoBase64 { get; set; }

        public int CompanyId { get; set; }
        public int RoleId { get; set; }
        public int? DepartmentId { get; set; }
        public int? SectionId { get; set; }
        public int? PositionId { get; set; }

        public string CompanyName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int RoleLevel { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string CompanyHierarchyPath { get; set; } = string.Empty;
        public string DepartmentHierarchyPath { get; set; } = string.Empty;

        [Display(Name = "Password Saat Ini")]
        [StringLength(200)]
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [Display(Name = "Password Baru")]
        [StringLength(200)]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [Display(Name = "Konfirmasi Password Baru")]
        [StringLength(200)]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Konfirmasi password tidak sama.")]
        public string? ConfirmPassword { get; set; }

        [Display(Name = "Tema Pribadi")]
        [StringLength(30)]
        public string? ThemePreference { get; set; }

        [Display(Name = "Font Utama")]
        [StringLength(80)]
        public string? FontPrimary { get; set; }

        [Display(Name = "Font Pendamping")]
        [StringLength(80)]
        public string? FontSecondary { get; set; }

        public IReadOnlyList<ProfileActivityItem> Activity { get; set; } = Array.Empty<ProfileActivityItem>();
        public IReadOnlyList<ProfileSessionItem> Sessions { get; set; } = Array.Empty<ProfileSessionItem>();
    }

    public class ProfileActivityItem
    {
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ProfileSessionItem
    {
        public int SessionId { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string DeviceLabel { get; set; } = string.Empty;
        public string LocationLabel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSeen { get; set; }
        public bool IsCurrent { get; set; }
    }
}
