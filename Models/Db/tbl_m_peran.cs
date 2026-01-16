using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_peran
{
    public int peran_id { get; set; }

    public string nama_peran { get; set; } = null!;

    public int level_akses { get; set; }

    public bool is_aktif { get; set; }

    public DateTime dibuat_pada { get; set; }

    public DateTime? diubah_pada { get; set; }

    public string? startup_url { get; set; }
}
