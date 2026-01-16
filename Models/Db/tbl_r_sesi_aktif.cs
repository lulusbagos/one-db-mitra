using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_sesi_aktif
    {
        public int sesi_id { get; set; }
        public int pengguna_id { get; set; }
        public string? username { get; set; }
        public string? ip_address { get; set; }
        public string? user_agent { get; set; }
        public DateTime dibuat_pada { get; set; }
        public DateTime? last_seen { get; set; }
        public bool is_aktif { get; set; }
        public DateTime? revoked_at { get; set; }
        public string? revoked_by { get; set; }
    }
}
