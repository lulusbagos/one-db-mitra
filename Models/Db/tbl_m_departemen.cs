using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_departemen
{
    public int departemen_id { get; set; }

    public int perusahaan_id { get; set; }

    public string nama_departemen { get; set; } = null!;

    public bool is_aktif { get; set; }

    public DateTime dibuat_pada { get; set; }

    public DateTime? diubah_pada { get; set; }
}
