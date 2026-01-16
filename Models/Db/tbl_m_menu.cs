using System;
using System.Collections.Generic;

namespace one_db_mitra.Models.Db;

public partial class tbl_m_menu
{
    public int menu_id { get; set; }

    public int? menu_induk_id { get; set; }

    public string kode_menu { get; set; } = null!;

    public string nama_tampil { get; set; } = null!;

    public string? ikon { get; set; }

    public string? url { get; set; }

    public int urutan { get; set; }

    public bool sembunyikan { get; set; }

    public bool tab_baru { get; set; }

    public bool is_aktif { get; set; }

    public DateTime dibuat_pada { get; set; }

    public DateTime? diubah_pada { get; set; }
}
