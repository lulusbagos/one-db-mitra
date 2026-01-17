namespace one_db_mitra.Models.Db
{
    public partial class vw_hirarki_perusahaan_4level
    {
        public int? id_owner { get; set; }
        public string? owner { get; set; }
        public int? id_main_contractor { get; set; }
        public string? main_contractor { get; set; }
        public int? id_sub_contractor { get; set; }
        public string? sub_contractor { get; set; }
        public int? id_vendor { get; set; }
        public string? vendor { get; set; }
    }
}
