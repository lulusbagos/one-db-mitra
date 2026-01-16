using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_seksi
{
    public int seksi_id { get; set; }

    public string? kode_seksi { get; set; }

    public string? nama_seksi { get; set; }

    public string? keterangan { get; set; }

    public string? status_seksi { get; set; }

    public int? departemen_id { get; set; }

    public int? perusahaan_id { get; set; }

    public bool? is_aktif { get; set; }

    public string? created_by { get; set; }

    public DateTime dibuat_pada { get; set; }

    public string? updated_by { get; set; }

    public DateTime? diubah_pada { get; set; }

    public string? deleted_by { get; set; }

    public DateTime? deleted_at { get; set; }
}
