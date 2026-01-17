using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_t_karyawan
    {
        public int karyawan_id { get; set; }
        public int personal_id { get; set; }
        public string no_nik { get; set; } = null!;
        public string? no_acr { get; set; }
        public DateTime? date_of_hire { get; set; }
        public int? poh_id { get; set; }
        public string? point_of_hire { get; set; }
        public DateTime? tanggal_masuk { get; set; }
        public DateTime? tanggal_aktif { get; set; }
        public string? email_kantor { get; set; }
        public string? url_foto { get; set; }
        public int perusahaan_id { get; set; }
        public int? departemen_id { get; set; }
        public int? seksi_id { get; set; }
        public int? jabatan_id { get; set; }
        public int? posisi_id { get; set; }
        public int? grade_id { get; set; }
        public string? id_karyawan_indexim { get; set; }
        public string? grade { get; set; }
        public int? klasifikasi_id { get; set; }
        public string? klasifikasi { get; set; }
        public int? roster_id { get; set; }
        public string? golongan_tipe { get; set; }
        public string? roster_kerja { get; set; }
        public int? status_karyawan_id { get; set; }
        public int? paybase_id { get; set; }
        public int? jenis_pajak_id { get; set; }
        public int? lokasi_penerimaan_id { get; set; }
        public string? lokasi_penerimaan { get; set; }
        public int? lokasi_kerja_id { get; set; }
        public string? lokasi_kerja { get; set; }
        public int? residence_id { get; set; }
        public string? status_residence { get; set; }
        public DateTime? tanggal_non_aktif { get; set; }
        public DateTime? alasan_non_aktif { get; set; }
        public int? jenis_perjanjian_id { get; set; }
        public string? no_perjanjian { get; set; }
        public DateTime? tanggal_ijin_mulai { get; set; }
        public DateTime? tanggal_ijin_akhir { get; set; }
        public bool status_aktif { get; set; }
        public int? perusahaan_m_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
        public string? deleted_by { get; set; }
        public DateTime? deleted_at { get; set; }
    }
}
