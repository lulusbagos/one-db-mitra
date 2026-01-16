using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_departemen
{
    public int departemen_id { get; set; }

    public string? kode_departemen { get; set; }

    public string? nama_departemen { get; set; }

    public string? keterangan { get; set; }

    public bool? is_aktif { get; set; }

    public int? perusahaan_id { get; set; }

    public string? created_by { get; set; }

    public DateTime? dibuat_pada { get; set; }
}
