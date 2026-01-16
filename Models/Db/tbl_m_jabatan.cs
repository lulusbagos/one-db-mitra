using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_jabatan
{
    public int jabatan_id { get; set; }

    public int seksi_id { get; set; }

    public string nama_jabatan { get; set; } = null!;

    public bool is_aktif { get; set; }

    public DateTime dibuat_pada { get; set; }

    public DateTime? diubah_pada { get; set; }

    public int perusahaan_id { get; set; }
}
