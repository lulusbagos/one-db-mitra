using System.ComponentModel.DataAnnotations;

namespace one_db_mitra.Models.Admin
{
    public class AppSettingViewModel
    {
        public int SettingId { get; set; }

        [Display(Name = "Nama Aplikasi")]
        [StringLength(150)]
        public string? AppName { get; set; }

        [Display(Name = "Nama Header")]
        [StringLength(200)]
        public string? HeaderName { get; set; }

        [Display(Name = "Teks Footer")]
        [StringLength(300)]
        public string? FooterText { get; set; }

        [Display(Name = "Logo (URL)")]
        [StringLength(300)]
        public string? LogoUrl { get; set; }

        [Display(Name = "Aktifkan Pengumuman")]
        public bool AnnouncementEnabled { get; set; }

        [Display(Name = "Judul Pengumuman")]
        [StringLength(150)]
        public string? AnnouncementTitle { get; set; }

        [Display(Name = "Isi Pengumuman")]
        [StringLength(600)]
        public string? AnnouncementMessage { get; set; }

        [Display(Name = "Tipe Pengumuman")]
        [StringLength(20)]
        public string? AnnouncementType { get; set; }

        [Display(Name = "Mulai Tayang")]
        public DateTime? AnnouncementStart { get; set; }

        [Display(Name = "Selesai Tayang")]
        public DateTime? AnnouncementEnd { get; set; }

        [Display(Name = "Font Utama")]
        [StringLength(80)]
        public string? FontPrimary { get; set; }

        [Display(Name = "Font Pendamping")]
        [StringLength(80)]
        public string? FontSecondary { get; set; }
    }
}
