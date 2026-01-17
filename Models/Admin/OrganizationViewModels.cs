using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Admin
{
    public class CompanyListItem
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public int? CompanyTypeId { get; set; }
        public string CompanyTypeName { get; set; } = string.Empty;
        public string ParentCompanyName { get; set; } = "-";
        public bool IsActive { get; set; }
        public bool DokumenBelumLengkap { get; set; }
        public string? RemarkDokumen { get; set; }
    }

    public class DepartmentListItem
    {
        public int DepartmentId { get; set; }
        public string DepartmentCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SectionListItem
    {
        public int SectionId { get; set; }
        public string SectionCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class PositionListItem
    {
        public int PositionId { get; set; }
        public string PositionCode { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CompanyEditViewModel
    {
        public int CompanyId { get; set; }

        [Display(Name = "Kode Perusahaan")]
        [StringLength(15)]
        public string? CompanyCode { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Nama Perusahaan")]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = "Alamat Perusahaan")]
        [StringLength(200)]
        public string? CompanyAddress { get; set; }

        [Display(Name = "Status Perusahaan")]
        [StringLength(5)]
        public string? CompanyStatus { get; set; }

        [Display(Name = "Tipe Perusahaan")]
        public int? CompanyTypeId { get; set; }

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

        [Display(Name = "Kode Departemen")]
        [StringLength(15)]
        public string? DepartmentCode { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Nama Departemen")]
        public string DepartmentName { get; set; } = string.Empty;

        [Display(Name = "Keterangan")]
        [StringLength(250)]
        public string? Description { get; set; }

        [Display(Name = "Perusahaan")]
        public int CompanyId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class SectionEditViewModel
    {
        public int SectionId { get; set; }

        [Display(Name = "Kode Section")]
        [StringLength(20)]
        public string? SectionCode { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Nama Section")]
        public string SectionName { get; set; } = string.Empty;

        [Display(Name = "Keterangan")]
        [StringLength(150)]
        public string? Description { get; set; }

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

        [Display(Name = "Kode Jabatan")]
        [StringLength(10)]
        public string? PositionCode { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Nama Jabatan")]
        public string PositionName { get; set; } = string.Empty;

        [Display(Name = "Keterangan")]
        [StringLength(600)]
        public string? Description { get; set; }

        [Display(Name = "Section")]
        public int? SectionId { get; set; }

        [Display(Name = "Perusahaan")]
        public int CompanyId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> SectionOptions { get; set; } = Array.Empty<SelectListItem>();
    }
}
