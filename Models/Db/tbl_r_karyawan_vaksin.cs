using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_karyawan_vaksin
    {
        public int vaksin_id { get; set; }
        public int karyawan_id { get; set; }
        public int personal_id { get; set; }
        public string no_nik { get; set; } = null!;
        public string? jenis_vaksin { get; set; }
        public string? dosis { get; set; }
        public DateTime? tanggal_vaksin { get; set; }
        public string? keterangan { get; set; }
        public string? file_path { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
    }
}
