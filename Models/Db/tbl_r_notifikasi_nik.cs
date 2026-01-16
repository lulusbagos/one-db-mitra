using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_notifikasi_nik
    {
        public int notifikasi_id { get; set; }
        public string no_nik { get; set; } = null!;
        public int? karyawan_id { get; set; }
        public string status_terdeteksi { get; set; } = null!;
        public string? pesan { get; set; }
        public DateTime dibuat_pada { get; set; }
        public string? dibuat_oleh { get; set; }
    }
}
