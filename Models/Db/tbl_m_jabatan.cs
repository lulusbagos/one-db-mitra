using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_jabatan
{
    public int jabatan_id { get; set; }

    public string? kode_jabatan { get; set; }

    public int? seksi_id { get; set; }

    public string? nama_jabatan { get; set; }

    public string? keterangan { get; set; }

    public string? status_jabatan { get; set; }

    public bool? is_aktif { get; set; }

    public int? perusahaan_id { get; set; }

    public string? created_by { get; set; }

    public DateTime? dibuat_pada { get; set; }

    public string? updated_by { get; set; }

    public DateTime? diubah_pada { get; set; }

    public string? deleted_at { get; set; }
}
