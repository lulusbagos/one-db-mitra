using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_t_karyawan
    {
        public int karyawan_id { get; set; }
        public int personal_id { get; set; }
        public string no_nik { get; set; } = null!;
        public string? no_acr { get; set; }
        public DateTime? tanggal_masuk { get; set; }
        public DateTime? tanggal_aktif { get; set; }
        public string? email_kantor { get; set; }
        public string? url_foto { get; set; }
        public int perusahaan_id { get; set; }
        public int? departemen_id { get; set; }
        public int? seksi_id { get; set; }
        public int? jabatan_id { get; set; }
        public string? id_karyawan_indexim { get; set; }
        public string? grade { get; set; }
        public string? klasifikasi { get; set; }
        public string? golongan_tipe { get; set; }
        public string? roster_kerja { get; set; }
        public string? point_of_hire { get; set; }
        public string? lokasi_penerimaan { get; set; }
        public string? lokasi_kerja { get; set; }
        public string? status_residence { get; set; }
        public DateTime? date_of_hire { get; set; }
        public bool status_aktif { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
        public string? deleted_by { get; set; }
        public DateTime? deleted_at { get; set; }
    }
}
