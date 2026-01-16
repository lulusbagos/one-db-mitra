using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_pengguna_peran
    {
        public int pengguna_peran_id { get; set; }
        public int pengguna_id { get; set; }
        public int peran_id { get; set; }
        public bool is_aktif { get; set; }
        public DateTime dibuat_pada { get; set; }
        public DateTime? diubah_pada { get; set; }
    }
}
