namespace one_db_mitra.Models.Db;

public partial class vw_hirarki_perusahaan_arrow
{
    public int id_perusahaan { get; set; }

    public string? kode_perusahaan { get; set; }

    public string? nama_perusahaan { get; set; }

    public int id_jenis_perusahaan { get; set; }

    public string? nama_tipe { get; set; }
}
