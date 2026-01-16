using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_perusahaan
{
    public int perusahaan_id { get; set; }

    public string? kode_perusahaan { get; set; }

    public string? nama_perusahaan { get; set; }

    public string? alamat_perusahaan { get; set; }

    public string? status_perusahaan { get; set; }

    public int? tipe_perusahaan_id { get; set; }

    public int? perusahaan_induk_id { get; set; }

    public bool is_aktif { get; set; }

    public string? created_by { get; set; }

    public DateTime dibuat_pada { get; set; }

    public string? updated_by { get; set; }

    public DateTime? diubah_pada { get; set; }

    public string? deleted_by { get; set; }

    public DateTime? deleted_at { get; set; }
}
