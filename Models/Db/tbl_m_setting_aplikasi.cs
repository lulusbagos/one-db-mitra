using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_m_setting_aplikasi
    {
        public int setting_id { get; set; }
        public string? nama_aplikasi { get; set; }
        public string? nama_header { get; set; }
        public string? footer_text { get; set; }
        public string? logo_url { get; set; }
        public bool announcement_enabled { get; set; }
        public string? announcement_title { get; set; }
        public string? announcement_message { get; set; }
        public string? announcement_type { get; set; }
        public DateTime? announcement_start { get; set; }
        public DateTime? announcement_end { get; set; }
        public string? font_primary { get; set; }
        public string? font_secondary { get; set; }
        public DateTime dibuat_pada { get; set; }
        public DateTime? diubah_pada { get; set; }
    }
}
