using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using one_db_mitra.Models.Db;

namespace one_db_mitra.Data;

public partial class OneDbMitraContext : DbContext
{
    public OneDbMitraContext()
    {
    }

    public OneDbMitraContext(DbContextOptions<OneDbMitraContext> options)
        : base(options)
    {
    }

    public virtual DbSet<tbl_m_departemen> tbl_m_departemen { get; set; }

    public virtual DbSet<tbl_m_email_notifikasi> tbl_m_email_notifikasi { get; set; }

    public virtual DbSet<tbl_m_email_setting> tbl_m_email_setting { get; set; }

    public virtual DbSet<tbl_m_jabatan> tbl_m_jabatan { get; set; }

    public virtual DbSet<tbl_t_karyawan> tbl_t_karyawan { get; set; }

    public virtual DbSet<tbl_m_menu> tbl_m_menu { get; set; }

    public virtual DbSet<tbl_m_personal> tbl_m_personal { get; set; }

    public virtual DbSet<tbl_m_pengguna> tbl_m_pengguna { get; set; }

    public virtual DbSet<tbl_m_peran> tbl_m_peran { get; set; }

    public virtual DbSet<tbl_m_perusahaan> tbl_m_perusahaan { get; set; }

    public virtual DbSet<tbl_m_seksi> tbl_m_seksi { get; set; }

    public virtual DbSet<tbl_m_tipe_perusahaan> tbl_m_tipe_perusahaan { get; set; }

    public virtual DbSet<tbl_m_setting_aplikasi> tbl_m_setting_aplikasi { get; set; }

    public virtual DbSet<tbl_r_menu_peran> tbl_r_menu_peran { get; set; }

    public virtual DbSet<tbl_r_menu_perusahaan> tbl_r_menu_perusahaan { get; set; }

    public virtual DbSet<tbl_r_pengguna_peran> tbl_r_pengguna_peran { get; set; }

    public virtual DbSet<tbl_r_audit_log> tbl_r_audit_log { get; set; }

    public virtual DbSet<tbl_r_karyawan_mutasi_request> tbl_r_karyawan_mutasi_request { get; set; }

    public virtual DbSet<tbl_r_karyawan_dokumen> tbl_r_karyawan_dokumen { get; set; }

    public virtual DbSet<tbl_r_karyawan_pendidikan> tbl_r_karyawan_pendidikan { get; set; }

    public virtual DbSet<tbl_r_karyawan_vaksin> tbl_r_karyawan_vaksin { get; set; }

    public virtual DbSet<tbl_r_karyawan_penempatan> tbl_r_karyawan_penempatan { get; set; }

    public virtual DbSet<tbl_r_karyawan_audit> tbl_r_karyawan_audit { get; set; }

    public virtual DbSet<tbl_r_karyawan_status> tbl_r_karyawan_status { get; set; }

    public virtual DbSet<tbl_r_notifikasi_nik> tbl_r_notifikasi_nik { get; set; }

    public virtual DbSet<tbl_r_sesi_aktif> tbl_r_sesi_aktif { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:PrimarySqlServer");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var activeConverter = new ValueConverter<bool?, string?>(
            v => v.HasValue ? (v.Value ? "1" : "0") : null,
            v => string.IsNullOrWhiteSpace(v)
                 ? (bool?)null
                 : v.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || v.Equals("aktif", StringComparison.OrdinalIgnoreCase));

        modelBuilder.Entity<tbl_m_departemen>(entity =>
        {
            entity.HasKey(e => e.departemen_id).HasName("PK__departem__EDE145685071FE7D");

            entity.ToTable("tbl_m_departemen");
            entity.Property(e => e.departemen_id).HasColumnName("id");
            entity.Property(e => e.perusahaan_id).HasColumnName("id_perusahaan");
            entity.Property(e => e.is_aktif).HasColumnName("status_aktif").HasConversion(activeConverter);
            entity.Property(e => e.dibuat_pada).HasColumnName("created_at").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.kode_departemen).HasMaxLength(15);
            entity.Property(e => e.nama_departemen).HasMaxLength(50);
            entity.Property(e => e.keterangan).HasMaxLength(250);
            entity.Property(e => e.created_by).HasMaxLength(30);
        });

        modelBuilder.Entity<tbl_m_email_notifikasi>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK_tbl_m_email_notifikasi");

            entity.Property(e => e.id).HasMaxLength(50);
            entity.Property(e => e.email_to).HasMaxLength(2000);
            entity.Property(e => e.email_cc).HasMaxLength(2000);
            entity.Property(e => e.email_bcc).HasMaxLength(50);
            entity.Property(e => e.subject).HasMaxLength(500);
            entity.Property(e => e.file_path).HasMaxLength(4000);
            entity.Property(e => e.created_by).HasMaxLength(4000);
            entity.Property(e => e.updated_by).HasMaxLength(4000);
        });

        modelBuilder.Entity<tbl_m_email_setting>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK_tbl_m_email_setting");

            entity.Property(e => e.smtp_host).HasMaxLength(200);
            entity.Property(e => e.smtp_username).HasMaxLength(200);
            entity.Property(e => e.smtp_password).HasMaxLength(200);
            entity.Property(e => e.from_email).HasMaxLength(200);
            entity.Property(e => e.from_name).HasMaxLength(200);
        });

        modelBuilder.Entity<tbl_m_jabatan>(entity =>
        {
            entity.HasKey(e => e.jabatan_id).HasName("PK__jabatan__95598114F9F96D53");
            entity.ToTable("tbl_m_jabatan");

            entity.Property(e => e.jabatan_id).HasColumnName("id");
            entity.Property(e => e.seksi_id).HasColumnName("id_seksi");
            entity.Property(e => e.perusahaan_id).HasColumnName("id_perusahaan");
            entity.Property(e => e.is_aktif).HasColumnName("status_aktif").HasConversion(activeConverter);
            entity.Property(e => e.dibuat_pada).HasColumnName("created_at").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.diubah_pada).HasColumnName("updated_at");
            entity.Property(e => e.kode_jabatan).HasMaxLength(10);
            entity.Property(e => e.nama_jabatan).HasMaxLength(50);
            entity.Property(e => e.keterangan).HasMaxLength(600);
            entity.Property(e => e.status_jabatan).HasMaxLength(5);
            entity.Property(e => e.created_by).HasMaxLength(30);
            entity.Property(e => e.updated_by).HasMaxLength(30);
            entity.Property(e => e.deleted_at).HasMaxLength(100);
        });

        modelBuilder.Entity<tbl_t_karyawan>(entity =>
        {
            entity.ToTable("tbl_t_karyawan");
            entity.HasKey(e => e.karyawan_id).HasName("PK_tbl_t_karyawan");

            entity.Property(e => e.karyawan_id).HasColumnName("id_karyawan");
            entity.Property(e => e.personal_id).HasColumnName("id_personal");
            entity.Property(e => e.perusahaan_id).HasColumnName("id_perusahaan");
            entity.Property(e => e.departemen_id).HasColumnName("id_departemen");
            entity.Property(e => e.seksi_id).HasColumnName("id_seksi");
            entity.Property(e => e.jabatan_id).HasColumnName("id_jabatan");
            entity.Property(e => e.point_of_hire).HasColumnName("place_of_hire");
            entity.Property(e => e.no_nik).HasMaxLength(50);
            entity.Property(e => e.no_acr).HasMaxLength(50);
            entity.Property(e => e.email_kantor).HasMaxLength(800);
            entity.Property(e => e.url_foto).HasColumnName("path_foto").HasMaxLength(200);
            entity.Property(e => e.id_karyawan_indexim).HasMaxLength(20);
            entity.Property(e => e.grade).HasMaxLength(50);
            entity.Property(e => e.klasifikasi).HasMaxLength(100);
            entity.Property(e => e.golongan_tipe).HasMaxLength(100);
            entity.Property(e => e.roster_kerja).HasMaxLength(50);
            entity.Property(e => e.point_of_hire).HasMaxLength(80);
            entity.Property(e => e.lokasi_penerimaan).HasMaxLength(150);
            entity.Property(e => e.lokasi_kerja).HasMaxLength(150);
            entity.Property(e => e.status_residence).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(50);
            entity.Property(e => e.updated_by).HasMaxLength(50);
            entity.Property(e => e.deleted_by).HasMaxLength(50);
        });

        modelBuilder.Entity<tbl_m_menu>(entity =>
        {
            entity.HasKey(e => e.menu_id).HasName("PK__menu__4CA0FADCC041DB5B");

            entity.HasIndex(e => e.kode_menu, "UX_menu_kode").IsUnique();

            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ikon).HasMaxLength(100);
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
            entity.Property(e => e.kode_menu).HasMaxLength(50);
            entity.Property(e => e.nama_tampil).HasMaxLength(120);
            entity.Property(e => e.url).HasMaxLength(250);
            entity.Property(e => e.urutan).HasDefaultValue(10);
        });

        modelBuilder.Entity<tbl_m_pengguna>(entity =>
        {
            entity.HasKey(e => e.pengguna_id).HasName("PK__pengguna__043010D08E8ED661");

            entity.HasIndex(e => e.username, "UX_pengguna_username").IsUnique();

            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.email).HasMaxLength(150);
            entity.Property(e => e.foto_url).HasMaxLength(500);
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
            entity.Property(e => e.kata_sandi).HasMaxLength(200);
            entity.Property(e => e.nama_lengkap).HasMaxLength(150);
            entity.Property(e => e.username).HasMaxLength(100);
            entity.Property(e => e.user_theme).HasMaxLength(30);
            entity.Property(e => e.user_font_primary).HasMaxLength(80);
            entity.Property(e => e.user_font_secondary).HasMaxLength(80);
        });

        modelBuilder.Entity<tbl_m_personal>(entity =>
        {
            entity.HasKey(e => e.personal_id).HasName("PK_tbl_m_personal");

            entity.Property(e => e.personal_id).HasColumnName("id_personal");
            entity.Property(e => e.no_ktp).HasMaxLength(30);
            entity.Property(e => e.no_kk).HasMaxLength(30);
            entity.Property(e => e.nama_lengkap).HasMaxLength(60);
            entity.Property(e => e.nama_alias).HasMaxLength(40);
            entity.Property(e => e.jenis_kelamin).HasMaxLength(10);
            entity.Property(e => e.tempat_lahir).HasMaxLength(80);
            entity.Property(e => e.status_nikah).HasMaxLength(30);
            entity.Property(e => e.warga_negara).HasMaxLength(10);
            entity.Property(e => e.email_pribadi).HasMaxLength(150);
            entity.Property(e => e.hp_1).HasMaxLength(30);
            entity.Property(e => e.hp_2).HasMaxLength(30);
            entity.Property(e => e.no_npwp).HasMaxLength(25);
            entity.Property(e => e.no_bpjs_tk).HasMaxLength(25);
            entity.Property(e => e.no_bpjs_kes).HasMaxLength(25);
            entity.Property(e => e.no_bpjs_pensiun).HasMaxLength(25);
            entity.Property(e => e.created_by).HasMaxLength(30);
            entity.Property(e => e.updated_by).HasMaxLength(30);
            entity.Property(e => e.deleted_by).HasMaxLength(30);
            entity.Property(e => e.alamat).HasMaxLength(500);
            entity.Property(e => e.provinsi).HasMaxLength(100);
            entity.Property(e => e.kabupaten).HasMaxLength(100);
            entity.Property(e => e.kecamatan).HasMaxLength(100);
            entity.Property(e => e.desa).HasMaxLength(100);
            entity.Property(e => e.kode_pos).HasMaxLength(20);
        });

        modelBuilder.Entity<tbl_m_peran>(entity =>
        {
            entity.HasKey(e => e.peran_id).HasName("PK__peran__98D43F6965C75CA7");

            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
            entity.Property(e => e.nama_peran).HasMaxLength(100);
            entity.Property(e => e.startup_url).HasMaxLength(250);
        });

        modelBuilder.Entity<tbl_m_perusahaan>(entity =>
        {
            entity.HasKey(e => e.perusahaan_id).HasName("PK__perusaha__AEAAB193AC1C1CCD");

            entity.ToTable("tbl_m_perusahaan");
            entity.Property(e => e.perusahaan_id).HasColumnName("id");
            entity.Property(e => e.is_aktif).HasColumnName("status_aktif").HasDefaultValue(true);
            entity.Property(e => e.dibuat_pada).HasColumnName("created_at").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.diubah_pada).HasColumnName("updated_at");
            entity.Property(e => e.kode_perusahaan).HasMaxLength(15);
            entity.Property(e => e.nama_perusahaan).HasMaxLength(200);
            entity.Property(e => e.alamat_perusahaan).HasMaxLength(200);
            entity.Property(e => e.status_perusahaan).HasMaxLength(5);
            entity.Property(e => e.created_by).HasMaxLength(50);
            entity.Property(e => e.updated_by).HasMaxLength(50);
            entity.Property(e => e.deleted_by).HasMaxLength(50);
            entity.Property(e => e.tipe_perusahaan_id).HasColumnName("tipe_perusahaan_id");
            entity.Property(e => e.perusahaan_induk_id).HasColumnName("perusahaan_induk_id");
        });

        modelBuilder.Entity<tbl_m_seksi>(entity =>
        {
            entity.HasKey(e => e.seksi_id).HasName("PK__seksi__7E4BD6190CCD357D");

            entity.ToTable("tbl_m_seksi");
            entity.Property(e => e.seksi_id).HasColumnName("id");
            entity.Property(e => e.departemen_id).HasColumnName("id_departemen");
            entity.Property(e => e.perusahaan_id).HasColumnName("id_perusahaan");
            entity.Property(e => e.is_aktif).HasColumnName("status_aktif");
            entity.Property(e => e.dibuat_pada).HasColumnName("created_at");
            entity.Property(e => e.diubah_pada).HasColumnName("updated_at");
            entity.Property(e => e.kode_seksi).HasMaxLength(20);
            entity.Property(e => e.nama_seksi).HasMaxLength(100);
            entity.Property(e => e.keterangan).HasMaxLength(150);
            entity.Property(e => e.status_seksi).HasMaxLength(5);
            entity.Property(e => e.created_by).HasMaxLength(30);
            entity.Property(e => e.updated_by).HasMaxLength(30);
            entity.Property(e => e.deleted_by).HasMaxLength(30);
        });

        modelBuilder.Entity<tbl_m_tipe_perusahaan>(entity =>
        {
            entity.HasKey(e => e.tipe_perusahaan_id).HasName("PK__tipe_per__F353ACE91F68A4A0");

            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
            entity.Property(e => e.nama_tipe).HasMaxLength(100);
        });

        modelBuilder.Entity<tbl_m_setting_aplikasi>(entity =>
        {
            entity.HasKey(e => e.setting_id).HasName("PK__setting__3249C9A3A7DA5A70");

            entity.Property(e => e.nama_aplikasi).HasMaxLength(150);
            entity.Property(e => e.nama_header).HasMaxLength(200);
            entity.Property(e => e.footer_text).HasMaxLength(300);
            entity.Property(e => e.logo_url).HasMaxLength(300);
            entity.Property(e => e.announcement_enabled).HasDefaultValue(false);
            entity.Property(e => e.announcement_title).HasMaxLength(150);
            entity.Property(e => e.announcement_message).HasMaxLength(600);
            entity.Property(e => e.announcement_type).HasMaxLength(20);
            entity.Property(e => e.font_primary).HasMaxLength(80);
            entity.Property(e => e.font_secondary).HasMaxLength(80);
            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_menu_peran>(entity =>
        {
            entity.HasKey(e => e.menu_peran_id).HasName("PK__menu_per__2E915C6ACBF77028");

            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
        });

        modelBuilder.Entity<tbl_r_menu_perusahaan>(entity =>
        {
            entity.HasKey(e => e.menu_perusahaan_id).HasName("PK__menu_per__E2B924B643C3ED63");

            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
        });

        modelBuilder.Entity<tbl_r_pengguna_peran>(entity =>
        {
            entity.HasKey(e => e.pengguna_peran_id).HasName("PK_tbl_r_pengguna_peran");

            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
        });

        modelBuilder.Entity<tbl_r_audit_log>(entity =>
        {
            entity.HasKey(e => e.audit_id).HasName("PK__audit_lo__E9367B1C9D3B4B12");

            entity.Property(e => e.aksi).HasMaxLength(50);
            entity.Property(e => e.entitas).HasMaxLength(80);
            entity.Property(e => e.kunci).HasMaxLength(120);
            entity.Property(e => e.deskripsi).HasMaxLength(400);
            entity.Property(e => e.username).HasMaxLength(100);
            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_karyawan_mutasi_request>(entity =>
        {
            entity.HasKey(e => e.request_id).HasName("PK_tbl_r_karyawan_mutasi_request");

            entity.Property(e => e.no_nik).HasMaxLength(50);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.disetujui_oleh).HasMaxLength(100);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_karyawan_dokumen>(entity =>
        {
            entity.ToTable("tbl_t_karyawan_dokumen");
            entity.HasKey(e => e.dokumen_id).HasName("PK_tbl_t_karyawan_dokumen");

            entity.Property(e => e.dokumen_id).HasColumnName("id_dokumen");
            entity.Property(e => e.karyawan_id).HasColumnName("id_karyawan");
            entity.Property(e => e.nama_dokumen).HasMaxLength(150);
            entity.Property(e => e.jenis_dokumen).HasMaxLength(150);
            entity.Property(e => e.file_path).HasMaxLength(500);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<tbl_r_karyawan_audit>(entity =>
        {
            entity.HasKey(e => e.audit_id).HasName("PK_tbl_r_karyawan_audit");

            entity.Property(e => e.no_nik).HasMaxLength(50);
            entity.Property(e => e.field_name).HasMaxLength(120);
            entity.Property(e => e.old_value).HasMaxLength(1000);
            entity.Property(e => e.new_value).HasMaxLength(1000);
            entity.Property(e => e.changed_by).HasMaxLength(100);
            entity.Property(e => e.source).HasMaxLength(30);
            entity.Property(e => e.changed_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_karyawan_pendidikan>(entity =>
        {
            entity.HasKey(e => e.pendidikan_id).HasName("PK_tbl_r_karyawan_pendidikan");

            entity.Property(e => e.nama_sekolah).HasMaxLength(80);
            entity.Property(e => e.fakultas).HasMaxLength(50);
            entity.Property(e => e.jurusan).HasMaxLength(50);
            entity.Property(e => e.file_pendukung).HasMaxLength(80);
            entity.Property(e => e.created_by).HasMaxLength(30);
            entity.Property(e => e.updated_by).HasMaxLength(30);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_karyawan_vaksin>(entity =>
        {
            entity.HasKey(e => e.vaksin_id).HasName("PK_tbl_r_karyawan_vaksin");

            entity.Property(e => e.no_nik).HasMaxLength(50);
            entity.Property(e => e.jenis_vaksin).HasMaxLength(100);
            entity.Property(e => e.dosis).HasMaxLength(30);
            entity.Property(e => e.keterangan).HasMaxLength(500);
            entity.Property(e => e.file_path).HasMaxLength(500);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_karyawan_penempatan>(entity =>
        {
            entity.HasKey(e => e.penempatan_id).HasName("PK_tbl_r_karyawan_penempatan");

            entity.Property(e => e.no_nik).HasMaxLength(50);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.sumber_perpindahan).HasMaxLength(30);
            entity.Property(e => e.keterangan).HasMaxLength(1000);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_karyawan_status>(entity =>
        {
            entity.HasKey(e => e.status_id).HasName("PK_tbl_r_karyawan_status");

            entity.Property(e => e.no_nik).HasMaxLength(50);
            entity.Property(e => e.status).HasMaxLength(30);
            entity.Property(e => e.kategori_blacklist).HasMaxLength(200);
            entity.Property(e => e.url_berkas).HasMaxLength(1000);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_notifikasi_nik>(entity =>
        {
            entity.HasKey(e => e.notifikasi_id).HasName("PK_tbl_r_notifikasi_nik");

            entity.Property(e => e.no_nik).HasMaxLength(50);
            entity.Property(e => e.status_terdeteksi).HasMaxLength(30);
            entity.Property(e => e.pesan).HasMaxLength(1000);
            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<tbl_r_sesi_aktif>(entity =>
        {
            entity.HasKey(e => e.sesi_id).HasName("PK__sesi_akt__79F7091E2E6E3C59");

            entity.Property(e => e.username).HasMaxLength(100);
            entity.Property(e => e.ip_address).HasMaxLength(80);
            entity.Property(e => e.user_agent).HasMaxLength(300);
            entity.Property(e => e.revoked_by).HasMaxLength(100);
            entity.Property(e => e.dibuat_pada).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.is_aktif).HasDefaultValue(true);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
