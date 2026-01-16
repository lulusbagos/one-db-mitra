using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_tipe_perusahaan
{
    public int tipe_perusahaan_id { get; set; }

    public string nama_tipe { get; set; } = null!;

    public int level_urut { get; set; }

    public bool is_aktif { get; set; }

    public DateTime dibuat_pada { get; set; }

    public DateTime? diubah_pada { get; set; }
}
