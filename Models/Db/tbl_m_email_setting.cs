using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_m_email_setting
    {
        public Guid id { get; set; }
        public string smtp_host { get; set; } = null!;
        public int smtp_port { get; set; }
        public string? smtp_username { get; set; }
        public string? smtp_password { get; set; }
        public string? from_email { get; set; }
        public string? from_name { get; set; }
        public bool enable_ssl { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? updated_by { get; set; }
    }
}
