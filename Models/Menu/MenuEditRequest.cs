using System;
using System.ComponentModel.DataAnnotations;

namespace one_db_mitra.Models.Menu
{
    public class MenuEditRequest
    {
        public int? ParentId { get; set; }

        [Required]
        [Display(Name = "Nama Menu")]
        [StringLength(80)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Kode Menu")]
        [StringLength(40)]
        public string MenuCode { get; set; } = string.Empty;

        [Display(Name = "Ikon (Bootstrap Icons)")]
        [StringLength(120)]
        public string IconKey { get; set; } = "bi-circle";

        [Display(Name = "Alamat Halaman")]
        [StringLength(250)]
        public string UrlPath { get; set; } = "#";

        [Display(Name = "Buka di Tab Baru")]
        public bool OpenInNewTab { get; set; }

        [Display(Name = "Sembunyikan Menu")]
        public bool IsHidden { get; set; }

        [Display(Name = "Urutan")]
        [Range(0, 999)]
        public int SortOrder { get; set; } = 10;

        [Display(Name = "Kategori Startup")]
        [StringLength(50)]
        public string StartupCategory { get; set; } = "default";
    }
}
