using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_karyawan_status
    {
        public int status_id { get; set; }
        public int karyawan_id { get; set; }
        public int personal_id { get; set; }
        public string no_nik { get; set; } = null!;
        public string status { get; set; } = null!;
        public string? alasan { get; set; }
        public string? kategori_blacklist { get; set; }
        public DateTime tanggal_mulai { get; set; }
        public DateTime? tanggal_selesai { get; set; }
        public string? url_berkas { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
    }
}
