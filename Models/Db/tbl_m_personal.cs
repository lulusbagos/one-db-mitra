using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_m_personal
    {
        public int personal_id { get; set; }
        public string? no_ktp { get; set; }
        public string? no_kk { get; set; }
        public string nama_lengkap { get; set; } = null!;
        public string? nama_alias { get; set; }
        public string? jenis_kelamin { get; set; }
        public string? tempat_lahir { get; set; }
        public DateTime? tanggal_lahir { get; set; }
        public int? id_agama { get; set; }
        public int? id_status_nikah { get; set; }
        public string? status_nikah { get; set; }
        public string? warga_negara { get; set; }
        public string? email_pribadi { get; set; }
        public string? hp_1 { get; set; }
        public string? hp_2 { get; set; }
        public string? nama_ibu { get; set; }
        public string? nama_ayah { get; set; }
        public string? no_npwp { get; set; }
        public string? no_bpjs_tk { get; set; }
        public string? no_bpjs_kes { get; set; }
        public string? no_bpjs_pensiun { get; set; }
        public int? id_pendidikan { get; set; }
        public string? nama_sekolah { get; set; }
        public string? fakultas { get; set; }
        public string? jurusan { get; set; }
        public string? file_pendukung { get; set; }
        public string? alamat { get; set; }
        public string? provinsi { get; set; }
        public string? kabupaten { get; set; }
        public string? kecamatan { get; set; }
        public string? desa { get; set; }
        public string? kode_pos { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
        public string? deleted_by { get; set; }
        public DateTime? deleted_at { get; set; }
    }
}
