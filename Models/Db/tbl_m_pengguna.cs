using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_pengguna
{
    public int pengguna_id { get; set; }

    public string username { get; set; } = null!;

    public string kata_sandi { get; set; } = null!;

    public string nama_lengkap { get; set; } = null!;

    public string? email { get; set; }

    public string? foto_url { get; set; }

    public int perusahaan_id { get; set; }

    public int peran_id { get; set; }

    public int? departemen_id { get; set; }

    public int? seksi_id { get; set; }

    public int? jabatan_id { get; set; }

    public bool is_aktif { get; set; }

    public DateTime? terakhir_login { get; set; }

    public DateTime dibuat_pada { get; set; }

    public DateTime? diubah_pada { get; set; }

    public string? user_theme { get; set; }

    public string? user_font_primary { get; set; }

    public string? user_font_secondary { get; set; }
}
