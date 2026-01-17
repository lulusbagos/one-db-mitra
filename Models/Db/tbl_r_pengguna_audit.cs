using System;

namespace one_db_mitra.Models.Db
{
    public partial class tbl_r_pengguna_audit
    {
        public int audit_id { get; set; }
        public int pengguna_id { get; set; }
        public string field_name { get; set; } = null!;
        public string? old_value { get; set; }
        public string? new_value { get; set; }
        public DateTime changed_at { get; set; }
        public string? changed_by { get; set; }
        public string? source { get; set; }
    }
}
