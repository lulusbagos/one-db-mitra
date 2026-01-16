using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Admin
{
    public class CompanyListItem
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyTypeName { get; set; } = string.Empty;
        public string ParentCompanyName { get; set; } = "-";
        public bool IsActive { get; set; }
    }

    public class DepartmentListItem
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SectionListItem
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class PositionListItem
    {
        public int PositionId { get; set; }
        public string PositionName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CompanyEditViewModel
    {
        public int CompanyId { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Nama Perusahaan")]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = "Tipe Perusahaan")]
        public int CompanyTypeId { get; set; }

        [Display(Name = "Perusahaan Induk")]
        public int? ParentCompanyId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> CompanyTypeOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> ParentCompanyOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class DepartmentEditViewModel
    {
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Nama Departemen")]
        public string DepartmentName { get; set; } = string.Empty;

        [Display(Name = "Perusahaan")]
        public int CompanyId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class SectionEditViewModel
    {
        public int SectionId { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Nama Section")]
        public string SectionName { get; set; } = string.Empty;

        [Display(Name = "Departemen")]
        public int DepartmentId { get; set; }

        [Display(Name = "Perusahaan")]
        public int CompanyId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DepartmentOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class PositionEditViewModel
    {
        public int PositionId { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Nama Jabatan")]
        public string PositionName { get; set; } = string.Empty;

        [Display(Name = "Section")]
        public int SectionId { get; set; }

        [Display(Name = "Perusahaan")]
        public int CompanyId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> SectionOptions { get; set; } = Array.Empty<SelectListItem>();
    }
}
