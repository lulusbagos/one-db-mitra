using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_karyawan_pendidikan
    {
        public int pendidikan_id { get; set; }
        public int personal_id { get; set; }
        public int? id_pendidikan { get; set; }
        public string? nama_sekolah { get; set; }
        public string? fakultas { get; set; }
        public string? jurusan { get; set; }
        public string? file_pendukung { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? created_by { get; set; }
        public string? updated_by { get; set; }
    }
}
