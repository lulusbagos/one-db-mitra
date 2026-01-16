using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_m_email_notifikasi
    {
        public string id { get; set; } = null!;
        public string? email_to { get; set; }
        public string? email_cc { get; set; }
        public string? email_bcc { get; set; }
        public string? subject { get; set; }
        public string? pesan_html { get; set; }
        public string? file_path { get; set; }
        public string? status { get; set; }
        public string? error_message { get; set; }
        public DateTime? created_at { get; set; }
        public string? created_by { get; set; }
        public DateTime? updated_at { get; set; }
        public string? updated_by { get; set; }
    }
}
