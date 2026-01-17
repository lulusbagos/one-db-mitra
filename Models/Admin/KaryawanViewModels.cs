using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace one_db_mitra.Models.Admin
{
    public class KaryawanListItem
    {
        public int KaryawanId { get; set; }
        public string NoNik { get; set; } = string.Empty;
        public string NamaLengkap { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? PhotoUrl { get; set; }
        public int DocumentCount { get; set; }
        public bool IsActive { get; set; }
        public string? StatusLabel { get; set; }
        public string? StatusType { get; set; }
    }

    public class KaryawanCreateViewModel
    {
        public int KaryawanId { get; set; }

        [Required]
        [Display(Name = "NIK")]
        [StringLength(50)]
        public string NoNik { get; set; } = string.Empty;

        [Display(Name = "No ACR")]
        [StringLength(50)]
        public string? NoAcr { get; set; }

        [Display(Name = "ID Karyawan")]
        [StringLength(20)]
        public string? IdKaryawan { get; set; }

        [Display(Name = "Warga Negara")]
        [StringLength(10)]
        public string? WargaNegara { get; set; }

        [Display(Name = "No KTP")]
        [StringLength(50)]
        public string? NoKtp { get; set; }

        [Display(Name = "No KK")]
        [StringLength(50)]
        public string? NoKk { get; set; }

        [Required]
        [Display(Name = "Nama Lengkap")]
        [StringLength(200)]
        public string NamaLengkap { get; set; } = string.Empty;

        [Display(Name = "Nama Alias")]
        [StringLength(200)]
        public string? NamaAlias { get; set; }

        [Display(Name = "Jenis Kelamin")]
        [StringLength(15)]
        public string? JenisKelamin { get; set; }

        [Display(Name = "Tempat Lahir")]
        [StringLength(100)]
        public string? TempatLahir { get; set; }

        [Display(Name = "Tanggal Lahir")]
        [DataType(DataType.Date)]
        public DateTime? TanggalLahir { get; set; }

        [Display(Name = "ID Agama")]
        public int? IdAgama { get; set; }

        [Display(Name = "ID Status Nikah")]
        public int? IdStatusNikah { get; set; }

        [Display(Name = "Status Nikah")]
        [StringLength(30)]
        public string? StatusNikah { get; set; }

        [Display(Name = "Email Pribadi")]
        [StringLength(100)]
        public string? EmailPribadi { get; set; }

        [Display(Name = "HP 1")]
        [StringLength(60)]
        public string? Hp1 { get; set; }

        [Display(Name = "HP 2")]
        [StringLength(60)]
        public string? Hp2 { get; set; }

        [Display(Name = "Nama Ibu")]
        [StringLength(50)]
        public string? NamaIbu { get; set; }

        [Display(Name = "Nama Ayah")]
        [StringLength(50)]
        public string? NamaAyah { get; set; }

        [Display(Name = "No NPWP")]
        [StringLength(25)]
        public string? NoNpwp { get; set; }

        [Display(Name = "No BPJS TK")]
        [StringLength(25)]
        public string? NoBpjsTk { get; set; }

        [Display(Name = "No BPJS Kesehatan")]
        [StringLength(25)]
        public string? NoBpjsKes { get; set; }

        [Display(Name = "No BPJS Pensiun")]
        [StringLength(25)]
        public string? NoBpjsPensiun { get; set; }

        [Display(Name = "ID Pendidikan (Terakhir)")]
        public int? IdPendidikan { get; set; }

        [Display(Name = "Nama Sekolah")]
        [StringLength(80)]
        public string? NamaSekolah { get; set; }

        [Display(Name = "Fakultas")]
        [StringLength(50)]
        public string? Fakultas { get; set; }

        [Display(Name = "Jurusan")]
        [StringLength(80)]
        public string? Jurusan { get; set; }

        [Display(Name = "File Pendukung")]
        [StringLength(80)]
        public string? FilePendukung { get; set; }

        public List<KaryawanPendidikanInput> PendidikanItems { get; set; } = new();

        [Display(Name = "Alamat")]
        [StringLength(500)]
        public string? Alamat { get; set; }

        [Display(Name = "Provinsi")]
        [StringLength(100)]
        public string? Provinsi { get; set; }

        [Display(Name = "Kabupaten/Kota")]
        [StringLength(100)]
        public string? Kabupaten { get; set; }

        [Display(Name = "Kecamatan")]
        [StringLength(100)]
        public string? Kecamatan { get; set; }

        [Display(Name = "Desa/Kelurahan")]
        [StringLength(100)]
        public string? Desa { get; set; }

        [Display(Name = "Kode Pos")]
        [StringLength(20)]
        public string? KodePos { get; set; }

        [Display(Name = "Tanggal Masuk")]
        [DataType(DataType.Date)]
        public DateTime? TanggalMasuk { get; set; }

        [Display(Name = "Tanggal Aktif")]
        [DataType(DataType.Date)]
        public DateTime? TanggalAktif { get; set; }

        [Display(Name = "Email Kantor")]
        [StringLength(200)]
        public string? EmailKantor { get; set; }

        [Display(Name = "Grade")]
        [StringLength(50)]
        public string? Grade { get; set; }

        [Display(Name = "ID Grade")]
        public int? IdGrade { get; set; }

        [Display(Name = "Klasifikasi")]
        [StringLength(100)]
        public string? Klasifikasi { get; set; }

        [Display(Name = "ID Klasifikasi")]
        public int? IdKlasifikasi { get; set; }

        [Display(Name = "Golongan (Tipe)")]
        [StringLength(100)]
        public string? GolonganTipe { get; set; }

        [Display(Name = "ID Status Karyawan")]
        public int? IdStatusKaryawan { get; set; }

        [Display(Name = "Roster Kerja")]
        [StringLength(50)]
        public string? RosterKerja { get; set; }

        [Display(Name = "ID Roster")]
        public int? IdRoster { get; set; }

        [Display(Name = "Point of Hire (POH)")]
        [StringLength(100)]
        public string? PointOfHire { get; set; }

        [Display(Name = "ID POH")]
        public int? IdPoh { get; set; }

        [Display(Name = "ID Paybase")]
        public int? IdPaybase { get; set; }

        [Display(Name = "ID Jenis Pajak")]
        public int? IdJenisPajak { get; set; }

        [Display(Name = "Lokasi Penerimaan")]
        [StringLength(150)]
        public string? LokasiPenerimaan { get; set; }

        [Display(Name = "ID Lokasi Penerimaan")]
        public int? IdLokasiPenerimaan { get; set; }

        [Display(Name = "Lokasi Kerja (Site)")]
        [StringLength(150)]
        public string? LokasiKerja { get; set; }

        [Display(Name = "ID Lokasi Kerja")]
        public int? IdLokasiKerja { get; set; }

        [Display(Name = "Status Residence")]
        [StringLength(100)]
        public string? StatusResidence { get; set; }

        [Display(Name = "ID Residence")]
        public int? IdResidence { get; set; }

        [Display(Name = "Date of Hire (DOH)")]
        [DataType(DataType.Date)]
        public DateTime? DateOfHire { get; set; }

        [Display(Name = "Perusahaan")]
        public int CompanyId { get; set; }

        [Display(Name = "Departemen")]
        public int? DepartmentId { get; set; }

        [Display(Name = "Section")]
        public int? SectionId { get; set; }

        [Display(Name = "Jabatan")]
        public int? PositionId { get; set; }

        [Display(Name = "ID Posisi")]
        public int? PosisiId { get; set; }

        [Display(Name = "ID Perusahaan Induk")]
        public int? PerusahaanMId { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Tanggal Nonaktif (Karyawan)")]
        [DataType(DataType.Date)]
        public DateTime? TanggalNonAktifKaryawan { get; set; }

        [Display(Name = "Tanggal Alasan Nonaktif")]
        [DataType(DataType.Date)]
        public DateTime? AlasanNonAktifKaryawan { get; set; }

        [Display(Name = "ID Jenis Perjanjian")]
        public int? IdJenisPerjanjian { get; set; }

        [Display(Name = "No Perjanjian")]
        [StringLength(50)]
        public string? NoPerjanjian { get; set; }

        [Display(Name = "Tanggal Ijin Mulai")]
        [DataType(DataType.Date)]
        public DateTime? TanggalIjinMulai { get; set; }

        [Display(Name = "Tanggal Ijin Akhir")]
        [DataType(DataType.Date)]
        public DateTime? TanggalIjinAkhir { get; set; }

        [Display(Name = "Nonaktifkan Karyawan")]
        public bool NonaktifEnabled { get; set; }

        [Display(Name = "Kategori Nonaktif")]
        [StringLength(100)]
        public string? NonaktifKategori { get; set; }

        [Display(Name = "Alasan Nonaktif")]
        [StringLength(300)]
        public string? NonaktifAlasan { get; set; }

        [Display(Name = "Tanggal Nonaktif")]
        [DataType(DataType.Date)]
        public DateTime? NonaktifTanggal { get; set; }

        [Display(Name = "Keterangan Nonaktif")]
        [StringLength(500)]
        public string? NonaktifKeterangan { get; set; }

        [Display(Name = "Blacklist")]
        public bool BlacklistEnabled { get; set; }

        [Display(Name = "Alasan Blacklist")]
        [StringLength(300)]
        public string? BlacklistAlasan { get; set; }

        [Display(Name = "Tanggal Blacklist")]
        [DataType(DataType.Date)]
        public DateTime? BlacklistTanggal { get; set; }

        public List<KaryawanPelanggaranInput> PelanggaranItems { get; set; } = new();

        public List<KaryawanVaksinInput> VaksinItems { get; set; } = new();

        [Display(Name = "Foto Karyawan")]
        public IFormFile? FotoFile { get; set; }

        [Display(Name = "Lampiran KTP")]
        public IFormFile? KtpFile { get; set; }

        [Display(Name = "Lampiran MCU")]
        public IFormFile? McuFile { get; set; }

        [Display(Name = "Lampiran Lainnya")]
        public IFormFile? OtherFile { get; set; }

        [Display(Name = "Dokumen Pendukung")]
        public List<IFormFile> DokumenFiles { get; set; } = new();

        public string? CurrentPhotoUrl { get; set; }

        public IEnumerable<SelectListItem> CompanyOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DepartmentOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> SectionOptions { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> PositionOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class KaryawanPelanggaranInput
    {
        [Display(Name = "Jenis Pelanggaran")]
        [StringLength(50)]
        public string? Jenis { get; set; }

        [Display(Name = "Kategori")]
        [StringLength(50)]
        public string? Kategori { get; set; }

        [Display(Name = "Tanggal Pelanggaran")]
        [DataType(DataType.Date)]
        public DateTime? Tanggal { get; set; }

        [Display(Name = "Keterangan")]
        [StringLength(500)]
        public string? Keterangan { get; set; }
    }

    public class KaryawanPelanggaranItem
    {
        public string Jenis { get; set; } = string.Empty;
        public DateTime? Tanggal { get; set; }
        public string? Keterangan { get; set; }
    }

    public class KaryawanVaksinInput
    {
        [Display(Name = "Jenis Vaksin")]
        [StringLength(100)]
        public string? Jenis { get; set; }

        [Display(Name = "Dosis")]
        [StringLength(30)]
        public string? Dosis { get; set; }

        [Display(Name = "Tanggal Vaksin")]
        [DataType(DataType.Date)]
        public DateTime? Tanggal { get; set; }

        [Display(Name = "Keterangan")]
        [StringLength(500)]
        public string? Keterangan { get; set; }
    }

    public class KaryawanDetailViewModel
    {
        public int KaryawanId { get; set; }
        public int PersonalId { get; set; }
        public string NoNik { get; set; } = string.Empty;
        public string NamaLengkap { get; set; } = string.Empty;
        public string? NamaAlias { get; set; }
        public string? WargaNegara { get; set; }
        public string? NoKtp { get; set; }
        public string? NoKk { get; set; }
        public string? PhotoUrl { get; set; }
        public string? EmailPribadi { get; set; }
        public string? EmailKantor { get; set; }
        public string? Phone { get; set; }
        public string? PhoneAlt { get; set; }
        public string? NamaIbu { get; set; }
        public string? NamaAyah { get; set; }
        public string? JenisKelamin { get; set; }
        public string? TempatLahir { get; set; }
        public DateTime? TanggalLahir { get; set; }
        public int? IdAgama { get; set; }
        public int? IdStatusNikah { get; set; }
        public string? StatusNikah { get; set; }
        public string? NoNpwp { get; set; }
        public string? NoBpjsTk { get; set; }
        public string? NoBpjsKes { get; set; }
        public string? NoBpjsPensiun { get; set; }
        public int? IdPendidikan { get; set; }
        public string? NamaSekolah { get; set; }
        public string? Fakultas { get; set; }
        public string? Jurusan { get; set; }
        public string? FilePendukung { get; set; }
        public string? Alamat { get; set; }
        public string? Provinsi { get; set; }
        public string? Kabupaten { get; set; }
        public string? Kecamatan { get; set; }
        public string? Desa { get; set; }
        public string? KodePos { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string? NoAcr { get; set; }
        public int? PohId { get; set; }
        public string? IdKaryawanIndexim { get; set; }
        public string? Grade { get; set; }
        public int? GradeId { get; set; }
        public string? Klasifikasi { get; set; }
        public int? KlasifikasiId { get; set; }
        public string? GolonganTipe { get; set; }
        public string? RosterKerja { get; set; }
        public int? RosterId { get; set; }
        public int? StatusKaryawanId { get; set; }
        public int? PaybaseId { get; set; }
        public int? JenisPajakId { get; set; }
        public string? PointOfHire { get; set; }
        public string? LokasiPenerimaan { get; set; }
        public int? LokasiPenerimaanId { get; set; }
        public string? LokasiKerja { get; set; }
        public int? LokasiKerjaId { get; set; }
        public string? StatusResidence { get; set; }
        public int? ResidenceId { get; set; }
        public DateTime? DateOfHire { get; set; }
        public bool IsActive { get; set; }
        public DateTime? TanggalMasuk { get; set; }
        public DateTime? TanggalAktif { get; set; }
        public DateTime? TanggalNonAktifKaryawan { get; set; }
        public DateTime? AlasanNonAktifKaryawan { get; set; }
        public int? JenisPerjanjianId { get; set; }
        public string? NoPerjanjian { get; set; }
        public DateTime? TanggalIjinMulai { get; set; }
        public DateTime? TanggalIjinAkhir { get; set; }
        public int? PosisiId { get; set; }
        public int? PerusahaanMId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public IReadOnlyList<KaryawanPenempatanItem> Penempatan { get; set; } = Array.Empty<KaryawanPenempatanItem>();
        public IReadOnlyList<KaryawanDokumenItem> Dokumen { get; set; } = Array.Empty<KaryawanDokumenItem>();
        public IReadOnlyList<KaryawanPendidikanItem> Pendidikan { get; set; } = Array.Empty<KaryawanPendidikanItem>();
        public IReadOnlyList<KaryawanVaksinItem> Vaksin { get; set; } = Array.Empty<KaryawanVaksinItem>();
        public IReadOnlyList<KaryawanPelanggaranItem> Pelanggaran { get; set; } = Array.Empty<KaryawanPelanggaranItem>();
        public IReadOnlyList<KaryawanAuditItem> AuditTrail { get; set; } = Array.Empty<KaryawanAuditItem>();
    }

    public class KaryawanPenempatanItem
    {
        public DateTime TanggalMulai { get; set; }
        public DateTime? TanggalSelesai { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Sumber { get; set; }
        public string? Keterangan { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class KaryawanDokumenItem
    {
        public string NamaDokumen { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class KaryawanPendidikanItem
    {
        public int? IdPendidikan { get; set; }
        public string? NamaSekolah { get; set; }
        public string? Fakultas { get; set; }
        public string? Jurusan { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class KaryawanVaksinItem
    {
        public string Jenis { get; set; } = string.Empty;
        public string Dosis { get; set; } = string.Empty;
        public DateTime? Tanggal { get; set; }
        public string? Keterangan { get; set; }
        public string? FilePath { get; set; }
    }

    public class KaryawanAuditItem
    {
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        public string? Source { get; set; }
    }

    public class KaryawanPendidikanInput
    {
        [Display(Name = "ID Pendidikan")]
        public int? IdPendidikan { get; set; }

        [Display(Name = "Nama Sekolah")]
        [StringLength(80)]
        public string? NamaSekolah { get; set; }

        [Display(Name = "Fakultas")]
        [StringLength(50)]
        public string? Fakultas { get; set; }

        [Display(Name = "Jurusan")]
        [StringLength(50)]
        public string? Jurusan { get; set; }
    }

    public class KaryawanImportRowViewModel
    {
        public int RowNumber { get; set; }
        public bool IsValid { get; set; }
        public string Action { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public string WargaNegara { get; set; } = string.Empty;
        public string NoKtp { get; set; } = string.Empty;
        public string NoNik { get; set; } = string.Empty;
        public string NamaLengkap { get; set; } = string.Empty;
        public string NamaAlias { get; set; } = string.Empty;
        public string JenisKelamin { get; set; } = string.Empty;
        public string TempatLahir { get; set; } = string.Empty;
        public DateTime? TanggalLahir { get; set; }
        public int? IdAgama { get; set; }
        public int? IdStatusNikah { get; set; }
        public string StatusNikah { get; set; } = string.Empty;
        public string NoKk { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public int? SectionId { get; set; }
        public string PositionName { get; set; } = string.Empty;
        public int? PositionId { get; set; }
        public string Grade { get; set; } = string.Empty;
        public string Klasifikasi { get; set; } = string.Empty;
        public string GolonganTipe { get; set; } = string.Empty;
        public string RosterKerja { get; set; } = string.Empty;
        public string PointOfHire { get; set; } = string.Empty;
        public string LokasiPenerimaan { get; set; } = string.Empty;
        public string LokasiKerja { get; set; } = string.Empty;
        public string StatusResidence { get; set; } = string.Empty;
        public DateTime? DateOfHire { get; set; }
        public DateTime? TanggalMasuk { get; set; }
        public DateTime? TanggalAktif { get; set; }
        public string EmailPribadi { get; set; } = string.Empty;
        public string EmailKantor { get; set; } = string.Empty;
        public string Hp1 { get; set; } = string.Empty;
        public string Hp2 { get; set; } = string.Empty;
        public string NamaIbu { get; set; } = string.Empty;
        public string NamaAyah { get; set; } = string.Empty;
        public string NoNpwp { get; set; } = string.Empty;
        public string NoBpjsTk { get; set; } = string.Empty;
        public string NoBpjsKes { get; set; } = string.Empty;
        public string NoBpjsPensiun { get; set; } = string.Empty;
        public int? IdPendidikan { get; set; }
        public string NamaSekolah { get; set; } = string.Empty;
        public string Fakultas { get; set; } = string.Empty;
        public string Jurusan { get; set; } = string.Empty;
        public string FilePendukung { get; set; } = string.Empty;
        public string Alamat { get; set; } = string.Empty;
        public string Provinsi { get; set; } = string.Empty;
        public string Kabupaten { get; set; } = string.Empty;
        public string Kecamatan { get; set; } = string.Empty;
        public string Desa { get; set; } = string.Empty;
        public string KodePos { get; set; } = string.Empty;
        public string NoAcr { get; set; } = string.Empty;
        public int? PohId { get; set; }
        public int? PosisiId { get; set; }
        public int? IdGrade { get; set; }
        public int? IdKlasifikasi { get; set; }
        public int? IdRoster { get; set; }
        public int? IdStatusKaryawan { get; set; }
        public int? IdPaybase { get; set; }
        public int? IdJenisPajak { get; set; }
        public int? IdLokasiPenerimaan { get; set; }
        public int? IdLokasiKerja { get; set; }
        public int? IdResidence { get; set; }
        public DateTime? TanggalNonAktifKaryawan { get; set; }
        public DateTime? AlasanNonAktifKaryawan { get; set; }
        public int? IdJenisPerjanjian { get; set; }
        public string NoPerjanjian { get; set; } = string.Empty;
        public DateTime? TanggalIjinMulai { get; set; }
        public DateTime? TanggalIjinAkhir { get; set; }
        public int? PerusahaanMId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class KaryawanImportPreviewViewModel
    {
        public string Token { get; set; } = string.Empty;
        public IReadOnlyList<KaryawanImportRowViewModel> Rows { get; set; } = Array.Empty<KaryawanImportRowViewModel>();
        public int Total => Rows.Count;
        public int ValidCount => Rows.Count(r => r.IsValid);
        public int InvalidCount => Rows.Count(r => !r.IsValid);
    }
}
