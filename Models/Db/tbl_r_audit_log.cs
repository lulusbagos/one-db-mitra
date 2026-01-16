using System;

namespace one_db_mitra.Models.Db;

public partial class tbl_r_audit_log
{
    public int audit_id { get; set; }
    public string aksi { get; set; } = null!;
    public string entitas { get; set; } = null!;
    public string? kunci { get; set; }
    public string? deskripsi { get; set; }
    public int? user_id { get; set; }
    public string? username { get; set; }
    public DateTime dibuat_pada { get; set; }
}
