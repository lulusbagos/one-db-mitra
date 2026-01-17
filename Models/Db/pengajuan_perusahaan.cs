using System;

namespace one_db_mitra.Models.Db;

public partial class tbl_r_pengajuan_perusahaan
{
    public int pengajuan_id { get; set; }
    public string kode_pengajuan { get; set; } = null!;
    public int? perusahaan_id { get; set; }
    public string nama_perusahaan { get; set; } = null!;
    public string email_perusahaan { get; set; } = null!;
    public int? tipe_perusahaan_id { get; set; }
    public int? perusahaan_induk_id { get; set; }
    public string? alamat_lengkap { get; set; }
    public string? telepon { get; set; }
    public string? contact_person { get; set; }
    public string? nomor_kontrak { get; set; }
    public string? durasi_kontrak { get; set; }
    public string? provinsi_code { get; set; }
    public string? provinsi_name { get; set; }
    public string? regency_code { get; set; }
    public string? regency_name { get; set; }
    public string? district_code { get; set; }
    public string? district_name { get; set; }
    public string? village_code { get; set; }
    public string? village_name { get; set; }
    public string? reviewer_id { get; set; }
    public string? reviewer_nik { get; set; }
    public string status_pengajuan { get; set; } = null!;
    public string? risk_category { get; set; }
    public string? reviewer_note { get; set; }
    public string? catatan_perusahaan { get; set; }
    public string? catatan_pengaju { get; set; }
    public bool is_aktif { get; set; }
    public bool is_legacy { get; set; }
    public string? created_by { get; set; }
    public DateTime created_at { get; set; }
    public string? updated_by { get; set; }
    public DateTime? updated_at { get; set; }
}

public partial class tbl_r_pengajuan_perusahaan_dokumen_tipe
{
    public int doc_type_id { get; set; }
    public string grup { get; set; } = null!;
    public string nama_dokumen { get; set; } = null!;
    public string? deskripsi { get; set; }
    public bool is_aktif { get; set; }
}

public partial class tbl_r_pengajuan_perusahaan_dokumen_wajib
{
    public int req_id { get; set; }
    public int pengajuan_id { get; set; }
    public int doc_type_id { get; set; }
    public bool wajib { get; set; }
    public string status { get; set; } = null!;
}

public partial class tbl_r_pengajuan_perusahaan_dokumen
{
    public int dokumen_id { get; set; }
    public int req_id { get; set; }
    public string? nama_file { get; set; }
    public string? path_file { get; set; }
    public string? uploaded_by { get; set; }
    public DateTime? uploaded_at { get; set; }
    public string status { get; set; } = null!;
    public string? catatan { get; set; }
}

public partial class tbl_r_pengajuan_perusahaan_review
{
    public int review_id { get; set; }
    public int pengajuan_id { get; set; }
    public string reviewer_id { get; set; } = null!;
    public string? reviewer_nik { get; set; }
    public string? risk_category { get; set; }
    public string? action_from { get; set; }
    public DateTime action_date { get; set; }
    public string status_review { get; set; } = null!;
    public string? catatan { get; set; }
    public bool approved_with_remark { get; set; }
    public string? remark_approve { get; set; }
    public string? bukti_approve_url { get; set; }
}

public partial class tbl_r_pengajuan_perusahaan_log
{
    public long log_id { get; set; }
    public int pengajuan_id { get; set; }
    public string aktivitas { get; set; } = null!;
    public string? performed_by { get; set; }
    public DateTime timestamp { get; set; }
}

public partial class tbl_r_pengajuan_perusahaan_link
{
    public int link_id { get; set; }
    public int pengajuan_id { get; set; }
    public string token { get; set; } = null!;
    public DateTime? expired_at { get; set; }
    public DateTime? used_at { get; set; }
    public string status { get; set; } = null!;
    public string? created_by { get; set; }
    public DateTime created_at { get; set; }
}
