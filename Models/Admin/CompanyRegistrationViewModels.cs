using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Admin;

public class PengajuanPerusahaanListItem
{
    public int PengajuanId { get; set; }
    public string KodePengajuan { get; set; } = string.Empty;
    public string NamaPerusahaan { get; set; } = string.Empty;
    public string EmailPerusahaan { get; set; } = string.Empty;
    public string TipePerusahaan { get; set; } = "-";
    public string StatusPengajuan { get; set; } = string.Empty;
    public string? ReviewerNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsLegacy { get; set; }
    public bool DokumenBelumLengkap { get; set; }
}

public class PengajuanPerusahaanCreateViewModel
{
    public int? PengajuanId { get; set; }

    [Display(Name = "Nama Perusahaan")]
    [Required]
    public string NamaPerusahaan { get; set; } = string.Empty;

    [Display(Name = "Email Perusahaan")]
    [Required]
    [EmailAddress]
    public string EmailPerusahaan { get; set; } = string.Empty;

    [Display(Name = "Tipe Perusahaan")]
    public int? TipePerusahaanId { get; set; }

    [Display(Name = "Perusahaan Induk")]
    public int? PerusahaanIndukId { get; set; }

    [Display(Name = "Alamat Lengkap")]
    public string? AlamatLengkap { get; set; }

    [Display(Name = "Telepon")]
    public string? Telepon { get; set; }

    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [Display(Name = "Nomor Kontrak")]
    public string? NomorKontrak { get; set; }

    [Display(Name = "Durasi Kontrak")]
    public string? DurasiKontrak { get; set; }

    [Display(Name = "Provinsi")]
    public string? ProvinsiName { get; set; }

    [Display(Name = "Kabupaten/Kota")]
    public string? RegencyName { get; set; }

    [Display(Name = "Kecamatan")]
    public string? DistrictName { get; set; }

    [Display(Name = "Desa")]
    public string? VillageName { get; set; }

    [Display(Name = "Catatan Pengaju")]
    public string? CatatanPengaju { get; set; }

    public IEnumerable<SelectListItem> TipePerusahaanOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> PerusahaanIndukOptions { get; set; } = Array.Empty<SelectListItem>();

    public List<PengajuanDokumenItem> Dokumen { get; set; } = new();
}

public class PengajuanDokumenItem
{
    public int ReqId { get; set; }
    public int DocTypeId { get; set; }
    public string Grup { get; set; } = string.Empty;
    public string NamaDokumen { get; set; } = string.Empty;
    public string? Deskripsi { get; set; }
    public bool Wajib { get; set; }
    public string Status { get; set; } = "required";
    public string? FileUrl { get; set; }
    public string? Catatan { get; set; }
    public IFormFile? UploadFile { get; set; }
    public bool IsRequired { get; set; }
    public bool SelectedRequired { get; set; }
}

public class PengajuanReviewViewModel
{
    public int PengajuanId { get; set; }
    public string KodePengajuan { get; set; } = string.Empty;
    public string NamaPerusahaan { get; set; } = string.Empty;
    public string EmailPerusahaan { get; set; } = string.Empty;
    public string StatusPengajuan { get; set; } = string.Empty;
    public string? ReviewerNote { get; set; }
    public bool DokumenBelumLengkap { get; set; }
    public string? CatatanPengaju { get; set; }
    public int? TipePerusahaanId { get; set; }
    public IEnumerable<SelectListItem> TipePerusahaanOptions { get; set; } = Array.Empty<SelectListItem>();

    [Display(Name = "Kategori Risiko")]
    public string? RiskCategory { get; set; }

    [Display(Name = "Catatan Safety")]
    public string? CatatanReview { get; set; }

    [Display(Name = "Approve dengan remark")]
    public bool ApprovedWithRemark { get; set; }

    [Display(Name = "Remark Approve")]
    public string? RemarkApprove { get; set; }

    [Display(Name = "Bukti Approve (URL)")]
    public string? BuktiApproveUrl { get; set; }

    public List<PengajuanDokumenItem> Dokumen { get; set; } = new();
    public List<PengajuanTimelineItem> Timeline { get; set; } = new();
    public string? PublicLinkUrl { get; set; }
}

public class PengajuanTimelineItem
{
    public string Judul { get; set; } = string.Empty;
    public string? Catatan { get; set; }
    public DateTime Waktu { get; set; }
    public string StatusBadge { get; set; } = "secondary";
}

public class PengajuanPublicUploadViewModel
{
    public string NamaPerusahaan { get; set; } = string.Empty;
    public string KodePengajuan { get; set; } = string.Empty;
    public int PengajuanId { get; set; }
    public string Token { get; set; } = string.Empty;
    public List<PengajuanDokumenItem> Dokumen { get; set; } = new();
}
