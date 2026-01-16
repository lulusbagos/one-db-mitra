using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_karyawan_dokumen
    {
        public int dokumen_id { get; set; }
        public int karyawan_id { get; set; }
        public string? nama_dokumen { get; set; }
        public string? jenis_dokumen { get; set; }
        public string file_path { get; set; } = null!;
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
    }
}
