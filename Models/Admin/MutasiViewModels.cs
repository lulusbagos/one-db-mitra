using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace one_db_mitra.Models.Admin
{
    public class MutasiRequestListItem
    {
        public int RequestId { get; set; }
        public int KaryawanId { get; set; }
        public string NoNik { get; set; } = string.Empty;
        public string NamaLengkap { get; set; } = string.Empty;
        public string CompanyAsal { get; set; } = string.Empty;
        public string CompanyTujuan { get; set; } = string.Empty;
        public DateTime TanggalPengajuan { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Catatan { get; set; }
        public string? DisetujuiOleh { get; set; }
        public DateTime? TanggalKeputusan { get; set; }
        public bool IsIncoming { get; set; }
        public bool CanApprove { get; set; }
    }

    public class MutasiIndexViewModel
    {
        public IReadOnlyList<MutasiRequestListItem> Incoming { get; set; } = Array.Empty<MutasiRequestListItem>();
        public IReadOnlyList<MutasiRequestListItem> Outgoing { get; set; } = Array.Empty<MutasiRequestListItem>();
    }

    public class MutasiCreateViewModel
    {
        [Required]
        [Display(Name = "NIK")]
        [StringLength(50)]
        public string NoNik { get; set; } = string.Empty;

        [Display(Name = "Catatan Pengajuan")]
        [StringLength(500)]
        public string? Catatan { get; set; }

        public string? NamaLengkap { get; set; }
        public string? CompanyAsal { get; set; }
        public string? CompanyTujuan { get; set; }
        public DateTime? TanggalAktifTerakhir { get; set; }
    }
}
