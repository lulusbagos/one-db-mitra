using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_karyawan_penempatan
    {
        public int penempatan_id { get; set; }
        public int karyawan_id { get; set; }
        public int personal_id { get; set; }
        public string no_nik { get; set; } = null!;
        public int? perusahaan_asal_id { get; set; }
        public int perusahaan_tujuan_id { get; set; }
        public int? departemen_id { get; set; }
        public int? seksi_id { get; set; }
        public int? jabatan_id { get; set; }
        public int? peran_id { get; set; }
        public DateTime tanggal_mulai { get; set; }
        public DateTime? tanggal_selesai { get; set; }
        public string status { get; set; } = null!;
        public string? sumber_perpindahan { get; set; }
        public string? keterangan { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
    }
}
