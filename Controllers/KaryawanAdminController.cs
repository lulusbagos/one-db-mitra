using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;
using ClosedXML.Excel;

namespace one_db_mitra.Controllers
{
    [Authorize]
    public class KaryawanAdminController : Controller
    {
        private readonly OneDbMitraContext _context;
        private readonly Services.Audit.AuditLogger _auditLogger;
        private readonly IMemoryCache _cache;
        private const string ImportCachePrefix = "karyawan-import::";

        public KaryawanAdminController(OneDbMitraContext context, Services.Audit.AuditLogger auditLogger, IMemoryCache cache)
        {
            _context = context;
            _auditLogger = auditLogger;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> Index(bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var companiesQuery = _context.tbl_m_perusahaan.AsNoTracking().AsQueryable();
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                companiesQuery = companiesQuery.Where(c => c.perusahaan_id == scope.CompanyId);
            }

            ViewBag.CompanyOptions = await companiesQuery
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem
                {
                    Value = c.perusahaan_id.ToString(),
                    Text = c.nama_perusahaan ?? "-"
                }).ToListAsync(cancellationToken);

            ViewBag.ActiveCount = 0;
            ViewBag.NonActiveCount = 0;
            ViewBag.ActiveOnly = activeOnly;
            ViewBag.IsOwner = IsOwner();
            return View(Array.Empty<KaryawanListItem>());
        }

        [HttpGet]
        public async Task<IActionResult> Data(
            string? search,
            [FromQuery(Name = "search[value]")] string? searchValue,
            int? companyId,
            bool activeOnly = true,
            int page = 1,
            int pageSize = 20,
            int? draw = null,
            int? start = null,
            int? length = null,
            CancellationToken cancellationToken = default)
        {
            if (start.HasValue && length.HasValue && length.Value > 0)
            {
                pageSize = length.Value;
                page = (start.Value / pageSize) + 1;
            }

            if (page < 1) page = 1;
            if (pageSize < 10) pageSize = 10;
            if (pageSize > 200) pageSize = 200;

            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var scopedQuery = ApplyKaryawanScope(_context.tbl_t_karyawan.AsNoTracking().AsQueryable(), scope);

            if (companyId.HasValue && companyId.Value > 0)
            {
                scopedQuery = scopedQuery.Where(k => k.perusahaan_id == companyId.Value);
            }

            var baseCountQuery = scopedQuery;
            if (activeOnly)
            {
                baseCountQuery = baseCountQuery.Where(k => k.status_aktif);
            }

            var activeCount = await scopedQuery.CountAsync(k => k.status_aktif, cancellationToken);
            var nonActiveCount = await scopedQuery.CountAsync(k => !k.status_aktif, cancellationToken);
            var blacklistCount = await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => scopedQuery.Select(k => k.karyawan_id).Contains(s.karyawan_id)
                            && s.status == "blacklist"
                            && s.tanggal_selesai == null)
                .Select(s => s.karyawan_id)
                .Distinct()
                .CountAsync(cancellationToken);

            var safetyCount = await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => scopedQuery.Select(k => k.karyawan_id).Contains(s.karyawan_id)
                            && s.status == "pelanggaran"
                            && s.kategori_blacklist == "Safety")
                .Select(s => s.karyawan_id)
                .Distinct()
                .CountAsync(cancellationToken);

            var hrCount = await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => scopedQuery.Select(k => k.karyawan_id).Contains(s.karyawan_id)
                            && s.status == "pelanggaran"
                            && s.kategori_blacklist == "HR")
                .Select(s => s.karyawan_id)
                .Distinct()
                .CountAsync(cancellationToken);

            var query = from k in scopedQuery
                        join p in _context.tbl_m_personal.AsNoTracking() on k.personal_id equals p.personal_id
                        join company in _context.tbl_m_perusahaan.AsNoTracking() on k.perusahaan_id equals company.perusahaan_id
                        join dept in _context.tbl_m_departemen.AsNoTracking() on k.departemen_id equals dept.departemen_id into deptJoin
                        from dept in deptJoin.DefaultIfEmpty()
                        join section in _context.tbl_m_seksi.AsNoTracking() on k.seksi_id equals section.seksi_id into sectionJoin
                        from sec in sectionJoin.DefaultIfEmpty()
                        join position in _context.tbl_m_jabatan.AsNoTracking() on k.jabatan_id equals position.jabatan_id into positionJoin
                        from pos in positionJoin.DefaultIfEmpty()
                        select new
                        {
                            k,
                            p,
                            company,
                            dept,
                            sec,
                            pos
                        };

            if (activeOnly)
            {
                query = query.Where(row => row.k.status_aktif);
            }

            var termValue = !string.IsNullOrWhiteSpace(searchValue) ? searchValue : search;
            if (!string.IsNullOrWhiteSpace(termValue))
            {
                var term = termValue.Trim();
                query = query.Where(row =>
                    row.k.no_nik.Contains(term) ||
                    row.p.nama_lengkap.Contains(term) ||
                    (row.p.email_pribadi ?? string.Empty).Contains(term) ||
                    (row.p.hp_1 ?? string.Empty).Contains(term) ||
                    (row.company.nama_perusahaan ?? string.Empty).Contains(term) ||
                    (row.dept != null ? row.dept.nama_departemen ?? string.Empty : string.Empty).Contains(term) ||
                    (row.pos != null ? row.pos.nama_jabatan ?? string.Empty : string.Empty).Contains(term));
            }

            var totalBeforeSearch = await baseCountQuery.CountAsync(cancellationToken);
            var total = await query.CountAsync(cancellationToken);

            var data = await query
                .OrderByDescending(row => row.k.karyawan_id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(row => new KaryawanListItem
                {
                    KaryawanId = row.k.karyawan_id,
                    NoNik = row.k.no_nik,
                    NamaLengkap = row.p.nama_lengkap,
                    Email = row.p.email_pribadi,
                    Phone = row.p.hp_1,
                    CompanyName = row.company.nama_perusahaan ?? string.Empty,
                    DepartmentName = row.dept != null ? row.dept.nama_departemen ?? "-" : "-",
                    SectionName = row.sec != null ? row.sec.nama_seksi ?? "-" : "-",
                    PositionName = row.pos != null ? row.pos.nama_jabatan ?? "-" : "-",
                    PhotoUrl = row.k.url_foto,
                    IsActive = row.k.status_aktif
                }).ToListAsync(cancellationToken);

            var ids = data.Select(d => d.KaryawanId).ToList();
            if (ids.Count > 0)
            {
                var docCounts = await _context.tbl_r_karyawan_dokumen.AsNoTracking()
                    .Where(d => ids.Contains(d.karyawan_id))
                    .GroupBy(d => d.karyawan_id)
                    .Select(g => new { karyawan_id = g.Key, total = g.Count() })
                    .ToDictionaryAsync(g => g.karyawan_id, g => g.total, cancellationToken);

                foreach (var item in data)
                {
                    if (docCounts.TryGetValue(item.KaryawanId, out var count))
                    {
                        item.DocumentCount = count;
                    }
                }

                var latestStatuses = await _context.tbl_r_karyawan_status.AsNoTracking()
                    .Where(s => ids.Contains(s.karyawan_id))
                    .GroupBy(s => s.karyawan_id)
                    .Select(g => g.OrderByDescending(x => x.tanggal_mulai)
                        .ThenByDescending(x => x.status_id)
                        .FirstOrDefault())
                    .ToListAsync(cancellationToken);

                var statusMap = latestStatuses
                    .Where(s => s != null)
                    .ToDictionary(s => s!.karyawan_id, s => s!);

                foreach (var item in data)
                {
                    if (!statusMap.TryGetValue(item.KaryawanId, out var status))
                    {
                        continue;
                    }

                    var label = status.status switch
                    {
                        "blacklist" => "Blacklist",
                        "pelanggaran" => "Pelanggaran",
                        "nonaktif" when string.Equals(status.kategori_blacklist, "Resign", StringComparison.OrdinalIgnoreCase) => "Resign",
                        "nonaktif" => "Nonaktif",
                        _ => null
                    };

                    item.StatusLabel = label;
                    item.StatusType = status.status;
                }
            }

            if (draw.HasValue)
            {
                return Json(new
                {
                    draw,
                    recordsTotal = totalBeforeSearch,
                    recordsFiltered = total,
                    data,
                    activeCount,
                    nonActiveCount,
                    blacklistCount,
                    safetyCount,
                    hrCount
                });
            }

            return Json(new
            {
                data,
                total,
                activeCount,
                nonActiveCount,
                blacklistCount,
                safetyCount,
                hrCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var model = new KaryawanCreateViewModel();
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CheckNik(string noNik, int? companyId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(noNik))
            {
                return Json(new { exists = false });
            }

            var matches = await (from k in _context.tbl_t_karyawan.AsNoTracking()
                                 join c in _context.tbl_m_perusahaan.AsNoTracking() on k.perusahaan_id equals c.perusahaan_id
                                 where k.no_nik == noNik
                                 select new { k.perusahaan_id, nama_perusahaan = c.nama_perusahaan ?? string.Empty })
                .ToListAsync(cancellationToken);

            var companies = matches.Select(m => m.nama_perusahaan).Distinct().ToList();
            var existsInCompany = companyId.HasValue && matches.Any(m => m.perusahaan_id == companyId.Value);

            return Json(new
            {
                exists = matches.Count > 0,
                existsInCompany,
                companies
            });
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("KARYAWAN");

            var headers = new[]
            {
                "WargaNegara",
                "NoKtp",
                "NoNik",
                "NoAcr",
                "NamaLengkap",
                "NamaAlias",
                "NamaIbu",
                "NamaAyah",
                "JenisKelamin",
                "TempatLahir",
                "TanggalLahir",
                "IdAgama",
                "IdStatusNikah",
                "StatusNikah",
                "NoNpwp",
                "NoBpjsTk",
                "NoBpjsKes",
                "NoBpjsPensiun",
                "IdPendidikan",
                "NamaSekolah",
                "Fakultas",
                "Jurusan",
                "FilePendukung",
                "NoKk",
                "EmailPribadi",
                "Hp1",
                "Hp2",
                "Alamat",
                "Provinsi",
                "Kabupaten",
                "Kecamatan",
                "Desa",
                "KodePos",
                "CompanyId",
                "CompanyName",
                "DepartmentId",
                "DepartmentName",
                "SectionId",
                "SectionName",
                "PositionId",
                "PositionName",
                "PosisiId",
                "IdGrade",
                "Grade",
                "IdKlasifikasi",
                "Klasifikasi",
                "IdStatusKaryawan",
                "GolonganTipe",
                "IdRoster",
                "RosterKerja",
                "IdPoh",
                "PointOfHire",
                "IdPaybase",
                "IdJenisPajak",
                "IdLokasiPenerimaan",
                "LokasiPenerimaan",
                "IdLokasiKerja",
                "LokasiKerja",
                "IdResidence",
                "StatusResidence",
                "DateOfHire",
                "TanggalMasuk",
                "TanggalAktif",
                "TanggalNonAktifKaryawan",
                "AlasanNonAktifKaryawan",
                "IdJenisPerjanjian",
                "NoPerjanjian",
                "TanggalIjinMulai",
                "TanggalIjinAkhir",
                "EmailKantor",
                "PerusahaanMId",
                "IsActive"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
                sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(14, 165, 233);
                sheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            var sample = new[]
            {
                "WNI",
                "1234567890123456",
                "NIK001",
                "ACR001",
                "Nama Contoh",
                "Alias Contoh",
                "Ibu Contoh",
                "Ayah Contoh",
                "Laki-laki",
                "Samarinda",
                "1990-01-01",
                "1",
                "1",
                "MENIKAH",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "1234567890123456",
                "contoh@email.com",
                "08123456789",
                "",
                "Jl. Contoh No.1",
                "KALIMANTAN TIMUR",
                "KUTAI TIMUR",
                "SANGATTA UTARA",
                "SANGATTA",
                "75300",
                "",
                "PT INDEXIM COALINDO",
                "",
                "OPERASIONAL",
                "",
                "PIT A",
                "",
                "OPERATOR",
                "",
                "",
                "3 SETARA",
                "",
                "TEKNISI",
                "",
                "NON STAF",
                "",
                "10:2",
                "",
                "LOCAL",
                "",
                "",
                "",
                "RING I (KALIORANG/KARANGAN/KAUBUN/SANGKULIRANG)",
                "",
                "SANGATTA (IC)",
                "",
                "RESIDENT (MESS)",
                "2024-01-15",
                "2024-01-15",
                "2024-01-15",
                "",
                "",
                "",
                "",
                "",
                "",
                "contoh@company.com",
                "",
                "1"
            };

            for (var i = 0; i < sample.Length; i++)
            {
                sheet.Cell(2, i + 1).Value = sample[i];
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_karyawan.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var baseQuery = ApplyKaryawanScope(_context.tbl_t_karyawan.AsNoTracking().AsQueryable(), scope);
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(k => k.status_aktif);
            }

            var rows = await (from k in baseQuery
                              join p in _context.tbl_m_personal.AsNoTracking() on k.personal_id equals p.personal_id
                              join company in _context.tbl_m_perusahaan.AsNoTracking() on k.perusahaan_id equals company.perusahaan_id
                              join dept in _context.tbl_m_departemen.AsNoTracking() on k.departemen_id equals dept.departemen_id into deptJoin
                              from dept in deptJoin.DefaultIfEmpty()
                              join section in _context.tbl_m_seksi.AsNoTracking() on k.seksi_id equals section.seksi_id into sectionJoin
                              from sec in sectionJoin.DefaultIfEmpty()
                              join position in _context.tbl_m_jabatan.AsNoTracking() on k.jabatan_id equals position.jabatan_id into positionJoin
                              from pos in positionJoin.DefaultIfEmpty()
                              let pendidikan = (from edu in _context.tbl_r_karyawan_pendidikan.AsNoTracking()
                                                where edu.personal_id == p.personal_id
                                                orderby edu.created_at descending
                                                select new
                                                {
                                                    edu.id_pendidikan,
                                                    edu.nama_sekolah,
                                                    edu.fakultas,
                                                    edu.jurusan
                                                }).FirstOrDefault()
                              orderby k.karyawan_id descending
                              select new
                              {
                                  p.warga_negara,
                                  p.no_ktp,
                                  k.no_nik,
                                  k.no_acr,
                                  p.nama_lengkap,
                                  p.nama_alias,
                                  p.nama_ibu,
                                  p.nama_ayah,
                                  p.jenis_kelamin,
                                  p.tempat_lahir,
                                  p.tanggal_lahir,
                                  p.id_agama,
                                  p.id_status_nikah,
                                  p.status_nikah,
                                  p.no_npwp,
                                  p.no_bpjs_tk,
                                  p.no_bpjs_kes,
                                  p.no_bpjs_pensiun,
                                  p.id_pendidikan,
                                  p.nama_sekolah,
                                  p.fakultas,
                                  p.jurusan,
                                  p.file_pendukung,
                                  pendidikan,
                                  p.no_kk,
                                  p.email_pribadi,
                                  p.hp_1,
                                  p.hp_2,
                                  p.alamat,
                                  p.provinsi,
                                  p.kabupaten,
                                  p.kecamatan,
                                  p.desa,
                                  p.kode_pos,
                                  company.perusahaan_id,
                                  company.nama_perusahaan,
                                  departemen_id = dept != null ? dept.departemen_id : (int?)null,
                                  departemen_nama = dept != null ? dept.nama_departemen : null,
                                  seksi_id = sec != null ? sec.seksi_id : (int?)null,
                                  seksi_nama = sec != null ? sec.nama_seksi : null,
                                  jabatan_id = pos != null ? pos.jabatan_id : (int?)null,
                                  jabatan_nama = pos != null ? pos.nama_jabatan : null,
                                  k.posisi_id,
                                  k.grade_id,
                                  k.grade,
                                  k.klasifikasi_id,
                                  k.klasifikasi,
                                  k.status_karyawan_id,
                                  k.golongan_tipe,
                                  k.roster_id,
                                  k.roster_kerja,
                                  k.poh_id,
                                  k.point_of_hire,
                                  k.paybase_id,
                                  k.jenis_pajak_id,
                                  k.lokasi_penerimaan_id,
                                  k.lokasi_penerimaan,
                                  k.lokasi_kerja_id,
                                  k.lokasi_kerja,
                                  k.residence_id,
                                  k.status_residence,
                                  k.date_of_hire,
                                  k.tanggal_masuk,
                                  k.tanggal_aktif,
                                  k.tanggal_non_aktif,
                                  k.alasan_non_aktif,
                                  k.jenis_perjanjian_id,
                                  k.no_perjanjian,
                                  k.tanggal_ijin_mulai,
                                  k.tanggal_ijin_akhir,
                                  k.email_kantor,
                                  k.perusahaan_m_id,
                                  k.status_aktif
                              }).ToListAsync(cancellationToken);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("KARYAWAN");

            var headers = new[]
            {
                "WargaNegara",
                "NoKtp",
                "NoNik",
                "NoAcr",
                "NamaLengkap",
                "NamaAlias",
                "NamaIbu",
                "NamaAyah",
                "JenisKelamin",
                "TempatLahir",
                "TanggalLahir",
                "IdAgama",
                "IdStatusNikah",
                "StatusNikah",
                "NoNpwp",
                "NoBpjsTk",
                "NoBpjsKes",
                "NoBpjsPensiun",
                "IdPendidikan",
                "NamaSekolah",
                "Fakultas",
                "Jurusan",
                "FilePendukung",
                "NoKk",
                "EmailPribadi",
                "Hp1",
                "Hp2",
                "Alamat",
                "Provinsi",
                "Kabupaten",
                "Kecamatan",
                "Desa",
                "KodePos",
                "CompanyId",
                "CompanyName",
                "DepartmentId",
                "DepartmentName",
                "SectionId",
                "SectionName",
                "PositionId",
                "PositionName",
                "PosisiId",
                "IdGrade",
                "Grade",
                "IdKlasifikasi",
                "Klasifikasi",
                "IdStatusKaryawan",
                "GolonganTipe",
                "IdRoster",
                "RosterKerja",
                "IdPoh",
                "PointOfHire",
                "IdPaybase",
                "IdJenisPajak",
                "IdLokasiPenerimaan",
                "LokasiPenerimaan",
                "IdLokasiKerja",
                "LokasiKerja",
                "IdResidence",
                "StatusResidence",
                "DateOfHire",
                "TanggalMasuk",
                "TanggalAktif",
                "TanggalNonAktifKaryawan",
                "AlasanNonAktifKaryawan",
                "IdJenisPerjanjian",
                "NoPerjanjian",
                "TanggalIjinMulai",
                "TanggalIjinAkhir",
                "EmailKantor",
                "PerusahaanMId",
                "IsActive"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
                sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(14, 165, 233);
                sheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            var rowIndex = 2;
            foreach (var row in rows)
            {
                sheet.Cell(rowIndex, 1).Value = row.warga_negara ?? "WNI";
                sheet.Cell(rowIndex, 2).Value = row.no_ktp ?? string.Empty;
                sheet.Cell(rowIndex, 3).Value = row.no_nik;
                sheet.Cell(rowIndex, 4).Value = row.no_acr ?? string.Empty;
                sheet.Cell(rowIndex, 5).Value = row.nama_lengkap;
                sheet.Cell(rowIndex, 6).Value = row.nama_alias ?? string.Empty;
                sheet.Cell(rowIndex, 7).Value = row.nama_ibu ?? string.Empty;
                sheet.Cell(rowIndex, 8).Value = row.nama_ayah ?? string.Empty;
                sheet.Cell(rowIndex, 9).Value = row.jenis_kelamin ?? string.Empty;
                sheet.Cell(rowIndex, 10).Value = row.tempat_lahir ?? string.Empty;
                sheet.Cell(rowIndex, 11).Value = row.tanggal_lahir?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 12).Value = row.id_agama?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 13).Value = row.id_status_nikah?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 14).Value = row.status_nikah ?? string.Empty;
                sheet.Cell(rowIndex, 15).Value = row.no_npwp ?? string.Empty;
                sheet.Cell(rowIndex, 16).Value = row.no_bpjs_tk ?? string.Empty;
                sheet.Cell(rowIndex, 17).Value = row.no_bpjs_kes ?? string.Empty;
                sheet.Cell(rowIndex, 18).Value = row.no_bpjs_pensiun ?? string.Empty;
                sheet.Cell(rowIndex, 19).Value = (row.id_pendidikan ?? row.pendidikan?.id_pendidikan)?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 20).Value = row.nama_sekolah ?? row.pendidikan?.nama_sekolah ?? string.Empty;
                sheet.Cell(rowIndex, 21).Value = row.fakultas ?? row.pendidikan?.fakultas ?? string.Empty;
                sheet.Cell(rowIndex, 22).Value = row.jurusan ?? row.pendidikan?.jurusan ?? string.Empty;
                sheet.Cell(rowIndex, 23).Value = row.file_pendukung ?? string.Empty;
                sheet.Cell(rowIndex, 24).Value = row.no_kk ?? string.Empty;
                sheet.Cell(rowIndex, 25).Value = row.email_pribadi ?? string.Empty;
                sheet.Cell(rowIndex, 26).Value = row.hp_1 ?? string.Empty;
                sheet.Cell(rowIndex, 27).Value = row.hp_2 ?? string.Empty;
                sheet.Cell(rowIndex, 28).Value = row.alamat ?? string.Empty;
                sheet.Cell(rowIndex, 29).Value = row.provinsi ?? string.Empty;
                sheet.Cell(rowIndex, 30).Value = row.kabupaten ?? string.Empty;
                sheet.Cell(rowIndex, 31).Value = row.kecamatan ?? string.Empty;
                sheet.Cell(rowIndex, 32).Value = row.desa ?? string.Empty;
                sheet.Cell(rowIndex, 33).Value = row.kode_pos ?? string.Empty;
                sheet.Cell(rowIndex, 34).Value = row.perusahaan_id;
                sheet.Cell(rowIndex, 35).Value = row.nama_perusahaan ?? string.Empty;
                sheet.Cell(rowIndex, 36).Value = row.departemen_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 37).Value = row.departemen_nama ?? string.Empty;
                sheet.Cell(rowIndex, 38).Value = row.seksi_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 39).Value = row.seksi_nama ?? string.Empty;
                sheet.Cell(rowIndex, 40).Value = row.jabatan_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 41).Value = row.jabatan_nama ?? string.Empty;
                sheet.Cell(rowIndex, 42).Value = row.posisi_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 43).Value = row.grade_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 44).Value = row.grade ?? string.Empty;
                sheet.Cell(rowIndex, 45).Value = row.klasifikasi_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 46).Value = row.klasifikasi ?? string.Empty;
                sheet.Cell(rowIndex, 47).Value = row.status_karyawan_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 48).Value = row.golongan_tipe ?? string.Empty;
                sheet.Cell(rowIndex, 49).Value = row.roster_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 50).Value = row.roster_kerja ?? string.Empty;
                sheet.Cell(rowIndex, 51).Value = row.poh_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 52).Value = row.point_of_hire ?? string.Empty;
                sheet.Cell(rowIndex, 53).Value = row.paybase_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 54).Value = row.jenis_pajak_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 55).Value = row.lokasi_penerimaan_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 56).Value = row.lokasi_penerimaan ?? string.Empty;
                sheet.Cell(rowIndex, 57).Value = row.lokasi_kerja_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 58).Value = row.lokasi_kerja ?? string.Empty;
                sheet.Cell(rowIndex, 59).Value = row.residence_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 60).Value = row.status_residence ?? string.Empty;
                sheet.Cell(rowIndex, 61).Value = row.date_of_hire?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 62).Value = row.tanggal_masuk?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 63).Value = row.tanggal_aktif?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 64).Value = row.tanggal_non_aktif?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 65).Value = row.alasan_non_aktif?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 66).Value = row.jenis_perjanjian_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 67).Value = row.no_perjanjian ?? string.Empty;
                sheet.Cell(rowIndex, 68).Value = row.tanggal_ijin_mulai?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 69).Value = row.tanggal_ijin_akhir?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 70).Value = row.email_kantor ?? string.Empty;
                sheet.Cell(rowIndex, 71).Value = row.perusahaan_m_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 72).Value = row.status_aktif ? "1" : "0";
                rowIndex++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "export_karyawan.xlsx");
        }

        [HttpGet]
        public IActionResult ImportPreview(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !_cache.TryGetValue($"{ImportCachePrefix}{token}", out KaryawanImportPreviewViewModel? preview) || preview is null)
            {
                TempData["AlertMessage"] = "Preview tidak ditemukan atau sudah kedaluwarsa.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            return View(preview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportPreview(IFormFile file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                TempData["AlertMessage"] = "File Excel belum dipilih.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var preview = await BuildImportPreviewAsync(file, scope, cancellationToken);
            preview.Token = Guid.NewGuid().ToString("N");

            _cache.Set($"{ImportCachePrefix}{preview.Token}", preview, TimeSpan.FromMinutes(30));
            return View(preview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmImport(string token, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token) || !_cache.TryGetValue($"{ImportCachePrefix}{token}", out KaryawanImportPreviewViewModel? preview) || preview is null)
            {
                TempData["AlertMessage"] = "Preview tidak ditemukan atau sudah kedaluwarsa.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var (inserted, updated, skipped, errors) = await ImportRowsAsync(preview.Rows.Where(r => r.IsValid).ToList(), cancellationToken);
            _cache.Remove($"{ImportCachePrefix}{token}");

            var message = $"Import selesai. Baru: {inserted}, Update: {updated}, Dilewati: {skipped}.";
            if (errors.Count > 0)
            {
                message += $" Detail: {string.Join(" | ", errors.Take(5))}";
            }

            TempData["AlertMessage"] = message;
            TempData["AlertType"] = errors.Count > 0 ? "warning" : "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                TempData["AlertMessage"] = "File Excel belum dipilih.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var inserted = 0;
            var updated = 0;
            var skipped = 0;
            var errors = new List<string>();

            using var workbook = new XLWorkbook(file.OpenReadStream());
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null)
            {
                TempData["AlertMessage"] = "Sheet Excel tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var headerRow = sheet.Row(1);
            var headerMap = headerRow.CellsUsed()
                .ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            for (var rowIndex = 2; rowIndex <= sheet.LastRowUsed().RowNumber(); rowIndex++)
            {
                var row = sheet.Row(rowIndex);
                if (row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString())))
                {
                    continue;
                }

                var warga = GetCell(row, headerMap, "WargaNegara");
                var noKtp = GetCell(row, headerMap, "NoKtp");
                var noNik = GetCell(row, headerMap, "NoNik");
                var namaLengkap = GetCell(row, headerMap, "NamaLengkap");
                var companyId = GetCellInt(row, headerMap, "CompanyId");
                var companyName = GetCell(row, headerMap, "CompanyName");

                if (companyId <= 0 && !string.IsNullOrWhiteSpace(companyName))
                {
                    companyId = await ResolveCompanyIdAsync(companyName, cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(noNik) || string.IsNullOrWhiteSpace(namaLengkap) || companyId <= 0)
                {
                    skipped++;
                    errors.Add($"Baris {rowIndex}: NoNik/NamaLengkap/CompanyId wajib diisi.");
                    continue;
                }

                if (!scope.IsOwner && scope.CompanyId > 0 && companyId != scope.CompanyId)
                {
                    skipped++;
                    errors.Add($"Baris {rowIndex}: CompanyId tidak sesuai akses.");
                    continue;
                }

                var wargaNormalized = string.IsNullOrWhiteSpace(warga) ? "WNI" : warga.Trim().ToUpperInvariant();
                if (wargaNormalized == "WNI")
                {
                    if (string.IsNullOrWhiteSpace(noKtp) || noKtp.Length != 16 || !noKtp.All(char.IsDigit))
                    {
                        skipped++;
                        errors.Add($"Baris {rowIndex}: No KTP WNI harus 16 digit.");
                        continue;
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(noKtp) || !noKtp.All(char.IsLetterOrDigit))
                    {
                        skipped++;
                        errors.Add($"Baris {rowIndex}: No paspor WNA harus huruf/angka.");
                        continue;
                    }
                }

                var conflictErrors = await GetPersonalConflictErrorsAsync(noKtp, GetCell(row, headerMap, "NoKk"), noNik, null, cancellationToken);
                if (conflictErrors.Count > 0)
                {
                    skipped++;
                    errors.Add($"Baris {rowIndex}: {string.Join(" ", conflictErrors.Select(e => e.Message))}");
                    continue;
                }

                var existingKaryawanSameCompany = await _context.tbl_t_karyawan.AsNoTracking()
                    .AnyAsync(k => k.no_nik == noNik && k.perusahaan_id == companyId, cancellationToken);

                var result = await ApplyImportRowAsync(new KaryawanImportRowViewModel
                {
                    WargaNegara = wargaNormalized,
                    NoKtp = noKtp,
                    NoNik = noNik,
                    NoAcr = GetCell(row, headerMap, "NoAcr"),
                    NamaLengkap = namaLengkap,
                    NamaAlias = GetCell(row, headerMap, "NamaAlias"),
                    NamaIbu = GetCell(row, headerMap, "NamaIbu"),
                    NamaAyah = GetCell(row, headerMap, "NamaAyah"),
                    JenisKelamin = GetCell(row, headerMap, "JenisKelamin"),
                    TempatLahir = GetCell(row, headerMap, "TempatLahir"),
                    TanggalLahir = GetCellDate(row, headerMap, "TanggalLahir"),
                    IdAgama = GetCellIntNullable(row, headerMap, "IdAgama"),
                    IdStatusNikah = GetCellIntNullable(row, headerMap, "IdStatusNikah"),
                    StatusNikah = GetCell(row, headerMap, "StatusNikah"),
                    NoKk = GetCell(row, headerMap, "NoKk"),
                    EmailPribadi = GetCell(row, headerMap, "EmailPribadi"),
                    Hp1 = GetCell(row, headerMap, "Hp1"),
                    Hp2 = GetCell(row, headerMap, "Hp2"),
                    NoNpwp = GetCell(row, headerMap, "NoNpwp"),
                    NoBpjsTk = GetCell(row, headerMap, "NoBpjsTk"),
                    NoBpjsKes = GetCell(row, headerMap, "NoBpjsKes"),
                    NoBpjsPensiun = GetCell(row, headerMap, "NoBpjsPensiun"),
                    IdPendidikan = GetCellIntNullable(row, headerMap, "IdPendidikan"),
                    NamaSekolah = GetCell(row, headerMap, "NamaSekolah"),
                    Fakultas = GetCell(row, headerMap, "Fakultas"),
                    Jurusan = GetCell(row, headerMap, "Jurusan"),
                    FilePendukung = GetCell(row, headerMap, "FilePendukung"),
                    Alamat = GetCell(row, headerMap, "Alamat"),
                    Provinsi = GetCell(row, headerMap, "Provinsi"),
                    Kabupaten = GetCell(row, headerMap, "Kabupaten"),
                    Kecamatan = GetCell(row, headerMap, "Kecamatan"),
                    Desa = GetCell(row, headerMap, "Desa"),
                    KodePos = GetCell(row, headerMap, "KodePos"),
                    CompanyId = companyId,
                    DepartmentId = ResolveOptionalId(GetCellIntNullable(row, headerMap, "DepartmentId"), await ResolveDepartmentIdAsync(companyId, GetCell(row, headerMap, "DepartmentName"), cancellationToken)),
                    SectionId = ResolveOptionalId(GetCellIntNullable(row, headerMap, "SectionId"), await ResolveSectionIdAsync(companyId, GetCell(row, headerMap, "DepartmentName"), GetCell(row, headerMap, "SectionName"), cancellationToken)),
                    PositionId = ResolveOptionalId(GetCellIntNullable(row, headerMap, "PositionId"), await ResolvePositionIdAsync(companyId, GetCell(row, headerMap, "DepartmentName"), GetCell(row, headerMap, "SectionName"), GetCell(row, headerMap, "PositionName"), cancellationToken)),
                    PosisiId = GetCellIntNullable(row, headerMap, "PosisiId"),
                    IdGrade = GetCellIntNullable(row, headerMap, "IdGrade"),
                    Grade = GetCell(row, headerMap, "Grade"),
                    IdKlasifikasi = GetCellIntNullable(row, headerMap, "IdKlasifikasi"),
                    Klasifikasi = GetCell(row, headerMap, "Klasifikasi"),
                    IdStatusKaryawan = GetCellIntNullable(row, headerMap, "IdStatusKaryawan"),
                    GolonganTipe = GetCell(row, headerMap, "GolonganTipe"),
                    IdRoster = GetCellIntNullable(row, headerMap, "IdRoster"),
                    RosterKerja = GetCell(row, headerMap, "RosterKerja"),
                    PohId = GetCellIntNullable(row, headerMap, "IdPoh"),
                    PointOfHire = GetCell(row, headerMap, "PointOfHire"),
                    IdPaybase = GetCellIntNullable(row, headerMap, "IdPaybase"),
                    IdJenisPajak = GetCellIntNullable(row, headerMap, "IdJenisPajak"),
                    IdLokasiPenerimaan = GetCellIntNullable(row, headerMap, "IdLokasiPenerimaan"),
                    LokasiPenerimaan = GetCell(row, headerMap, "LokasiPenerimaan"),
                    IdLokasiKerja = GetCellIntNullable(row, headerMap, "IdLokasiKerja"),
                    LokasiKerja = GetCell(row, headerMap, "LokasiKerja"),
                    IdResidence = GetCellIntNullable(row, headerMap, "IdResidence"),
                    StatusResidence = GetCell(row, headerMap, "StatusResidence"),
                    DateOfHire = GetCellDate(row, headerMap, "DateOfHire"),
                    TanggalMasuk = GetCellDate(row, headerMap, "TanggalMasuk"),
                    TanggalAktif = GetCellDate(row, headerMap, "TanggalAktif"),
                    TanggalNonAktifKaryawan = GetCellDate(row, headerMap, "TanggalNonAktifKaryawan"),
                    AlasanNonAktifKaryawan = GetCellDate(row, headerMap, "AlasanNonAktifKaryawan"),
                    IdJenisPerjanjian = GetCellIntNullable(row, headerMap, "IdJenisPerjanjian"),
                    NoPerjanjian = GetCell(row, headerMap, "NoPerjanjian"),
                    TanggalIjinMulai = GetCellDate(row, headerMap, "TanggalIjinMulai"),
                    TanggalIjinAkhir = GetCellDate(row, headerMap, "TanggalIjinAkhir"),
                    EmailKantor = GetCell(row, headerMap, "EmailKantor"),
                    PerusahaanMId = GetCellIntNullable(row, headerMap, "PerusahaanMId"),
                    IsActive = GetCellBool(row, headerMap, "IsActive", true)
                }, existingKaryawanSameCompany, cancellationToken);

                if (result == ImportRowResult.Inserted)
                {
                    inserted++;
                }
                else if (result == ImportRowResult.Updated)
                {
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }

            var message = $"Import selesai. Baru: {inserted}, Update: {updated}, Dilewati: {skipped}.";
            if (errors.Count > 0)
            {
                message += $" Detail: {string.Join(" | ", errors.Take(5))}";
            }

            TempData["AlertMessage"] = message;
            TempData["AlertType"] = errors.Count > 0 ? "warning" : "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        {
            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var canAccess = await ApplyKaryawanScope(_context.tbl_t_karyawan.AsNoTracking()
                    .Where(k => k.karyawan_id == id), scope)
                .AnyAsync(cancellationToken);
            if (!canAccess)
            {
                TempData["AlertMessage"] = "Tidak memiliki akses ke data karyawan ini.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var header = await (from k in _context.tbl_t_karyawan.AsNoTracking()
                                join p in _context.tbl_m_personal.AsNoTracking() on k.personal_id equals p.personal_id
                                join company in _context.tbl_m_perusahaan.AsNoTracking() on k.perusahaan_id equals company.perusahaan_id
                                join dept in _context.tbl_m_departemen.AsNoTracking() on k.departemen_id equals dept.departemen_id into deptJoin
                                from dept in deptJoin.DefaultIfEmpty()
                                join section in _context.tbl_m_seksi.AsNoTracking() on k.seksi_id equals section.seksi_id into sectionJoin
                                from sec in sectionJoin.DefaultIfEmpty()
                                join position in _context.tbl_m_jabatan.AsNoTracking() on k.jabatan_id equals position.jabatan_id into positionJoin
                                from pos in positionJoin.DefaultIfEmpty()
                                where k.karyawan_id == id
                                  select new KaryawanDetailViewModel
                                  {
                                      KaryawanId = k.karyawan_id,
                                      PersonalId = p.personal_id,
                                      NoNik = k.no_nik,
                                      NamaLengkap = p.nama_lengkap,
                                      NamaAlias = p.nama_alias,
                                      WargaNegara = p.warga_negara,
                                      NoKtp = p.no_ktp,
                                      NoKk = p.no_kk,
                                      PhotoUrl = k.url_foto,
                                      EmailPribadi = p.email_pribadi,
                                      EmailKantor = k.email_kantor,
                                      Phone = p.hp_1,
                                      PhoneAlt = p.hp_2,
                                      NamaIbu = p.nama_ibu,
                                      NamaAyah = p.nama_ayah,
                                      JenisKelamin = p.jenis_kelamin,
                                      TempatLahir = p.tempat_lahir,
                                      TanggalLahir = p.tanggal_lahir,
                                      IdAgama = p.id_agama,
                                      IdStatusNikah = p.id_status_nikah,
                                      StatusNikah = p.status_nikah,
                                      NoNpwp = p.no_npwp,
                                      NoBpjsTk = p.no_bpjs_tk,
                                      NoBpjsKes = p.no_bpjs_kes,
                                      NoBpjsPensiun = p.no_bpjs_pensiun,
                                      IdPendidikan = p.id_pendidikan,
                                      NamaSekolah = p.nama_sekolah,
                                      Fakultas = p.fakultas,
                                      Jurusan = p.jurusan,
                                      FilePendukung = p.file_pendukung,
                                      Alamat = p.alamat,
                                      Provinsi = p.provinsi,
                                      Kabupaten = p.kabupaten,
                                      Kecamatan = p.kecamatan,
                                      Desa = p.desa,
                                      KodePos = p.kode_pos,
                                      CompanyName = company.nama_perusahaan ?? string.Empty,
                                      DepartmentName = dept != null ? dept.nama_departemen ?? "-" : "-",
                                      SectionName = sec != null ? sec.nama_seksi ?? "-" : "-",
                                      PositionName = pos != null ? pos.nama_jabatan ?? "-" : "-",
                                      NoAcr = k.no_acr,
                                      PohId = k.poh_id,
                                      IdKaryawanIndexim = k.id_karyawan_indexim,
                                      Grade = k.grade,
                                      GradeId = k.grade_id,
                                      Klasifikasi = k.klasifikasi,
                                      KlasifikasiId = k.klasifikasi_id,
                                      GolonganTipe = k.golongan_tipe,
                                      RosterKerja = k.roster_kerja,
                                      RosterId = k.roster_id,
                                      StatusKaryawanId = k.status_karyawan_id,
                                      PaybaseId = k.paybase_id,
                                      JenisPajakId = k.jenis_pajak_id,
                                      PointOfHire = k.point_of_hire,
                                      LokasiPenerimaan = k.lokasi_penerimaan,
                                      LokasiPenerimaanId = k.lokasi_penerimaan_id,
                                      LokasiKerja = k.lokasi_kerja,
                                      LokasiKerjaId = k.lokasi_kerja_id,
                                      StatusResidence = k.status_residence,
                                      ResidenceId = k.residence_id,
                                      DateOfHire = k.date_of_hire,
                                      IsActive = k.status_aktif,
                                      TanggalMasuk = k.tanggal_masuk,
                                      TanggalAktif = k.tanggal_aktif,
                                      TanggalNonAktifKaryawan = k.tanggal_non_aktif,
                                      AlasanNonAktifKaryawan = k.alasan_non_aktif,
                                      JenisPerjanjianId = k.jenis_perjanjian_id,
                                      NoPerjanjian = k.no_perjanjian,
                                      TanggalIjinMulai = k.tanggal_ijin_mulai,
                                      TanggalIjinAkhir = k.tanggal_ijin_akhir,
                                      PosisiId = k.posisi_id,
                                      PerusahaanMId = k.perusahaan_m_id,
                                      CreatedAt = k.created_at,
                                      UpdatedAt = k.updated_at,
                                      CreatedBy = k.created_by,
                                      UpdatedBy = k.updated_by
                                  }).FirstOrDefaultAsync(cancellationToken);

            if (header is null)
            {
                TempData["AlertMessage"] = "Data karyawan tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var penempatanQuery = from p in _context.tbl_r_karyawan_penempatan.AsNoTracking()
                                    join company in _context.tbl_m_perusahaan.AsNoTracking() on p.perusahaan_tujuan_id equals company.perusahaan_id
                                    join dept in _context.tbl_m_departemen.AsNoTracking() on p.departemen_id equals dept.departemen_id into deptJoin
                                    from dept in deptJoin.DefaultIfEmpty()
                                    join section in _context.tbl_m_seksi.AsNoTracking() on p.seksi_id equals section.seksi_id into sectionJoin
                                    from sec in sectionJoin.DefaultIfEmpty()
                                    join position in _context.tbl_m_jabatan.AsNoTracking() on p.jabatan_id equals position.jabatan_id into positionJoin
                                    from pos in positionJoin.DefaultIfEmpty()
                                    where p.karyawan_id == id
                                    select new KaryawanPenempatanItem
                                    {
                                        TanggalMulai = p.tanggal_mulai,
                                        TanggalSelesai = p.tanggal_selesai,
                                        CompanyName = company.nama_perusahaan ?? string.Empty,
                                        DepartmentName = dept != null ? dept.nama_departemen ?? "-" : "-",
                                        SectionName = sec != null ? sec.nama_seksi ?? "-" : "-",
                                        PositionName = pos != null ? pos.nama_jabatan ?? "-" : "-",
                                        Status = p.status,
                                        Sumber = p.sumber_perpindahan,
                                        Keterangan = p.keterangan,
                                        CreatedBy = p.created_by
                                    };

            if (from.HasValue)
            {
                penempatanQuery = penempatanQuery.Where(p => p.TanggalMulai.Date >= from.Value.Date);
            }
            if (to.HasValue)
            {
                penempatanQuery = penempatanQuery.Where(p => p.TanggalMulai.Date <= to.Value.Date);
            }

            var penempatan = await penempatanQuery
                .OrderByDescending(p => p.TanggalMulai)
                .ToListAsync(cancellationToken);

            var dokumen = await _context.tbl_r_karyawan_dokumen.AsNoTracking()
                .Where(d => d.karyawan_id == id)
                .OrderByDescending(d => d.created_at)
                .Select(d => new KaryawanDokumenItem
                {
                    NamaDokumen = d.nama_dokumen ?? "Dokumen",
                    FilePath = d.file_path,
                    CreatedAt = d.created_at,
                    CreatedBy = d.created_by
                }).ToListAsync(cancellationToken);

            var pendidikan = await _context.tbl_r_karyawan_pendidikan.AsNoTracking()
                .Where(p => p.personal_id == header.PersonalId)
                .OrderByDescending(p => p.created_at)
                .Select(p => new KaryawanPendidikanItem
                {
                    IdPendidikan = p.id_pendidikan,
                    NamaSekolah = p.nama_sekolah,
                    Fakultas = p.fakultas,
                    Jurusan = p.jurusan,
                    CreatedAt = p.created_at,
                    CreatedBy = p.created_by
                }).ToListAsync(cancellationToken);

            var vaksin = await _context.tbl_r_karyawan_vaksin.AsNoTracking()
                .Where(v => v.karyawan_id == id)
                .OrderByDescending(v => v.tanggal_vaksin)
                .Select(v => new KaryawanVaksinItem
                {
                    Jenis = v.jenis_vaksin ?? "-",
                    Dosis = v.dosis ?? "-",
                    Tanggal = v.tanggal_vaksin,
                    Keterangan = v.keterangan,
                    FilePath = v.file_path
                }).ToListAsync(cancellationToken);

            var pelanggaran = await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => s.karyawan_id == id && s.status == "pelanggaran")
                .OrderByDescending(s => s.tanggal_mulai)
                .Select(s => new KaryawanPelanggaranItem
                {
                    Jenis = s.kategori_blacklist ?? "Lainnya",
                    Tanggal = s.tanggal_mulai,
                    Keterangan = s.alasan
                }).ToListAsync(cancellationToken);

            var auditTrail = await _context.tbl_r_karyawan_audit.AsNoTracking()
                .Where(a => a.karyawan_id == id)
                .OrderByDescending(a => a.changed_at)
                .Select(a => new KaryawanAuditItem
                {
                    FieldName = a.field_name,
                    OldValue = a.old_value,
                    NewValue = a.new_value,
                    ChangedAt = a.changed_at,
                    ChangedBy = a.changed_by,
                    Source = a.source
                }).ToListAsync(cancellationToken);

            header.Penempatan = penempatan;
            header.Dokumen = dokumen;
            header.Pendidikan = pendidikan;
            header.Vaksin = vaksin;
            header.Pelanggaran = pelanggaran;
            header.AuditTrail = auditTrail;

            ViewBag.FilterFrom = from;
            ViewBag.FilterTo = to;
            ViewBag.IsOwner = IsOwner();
            ViewBag.IsBlacklisted = await IsNikBlacklistedAsync(header.NoNik, cancellationToken);
            ViewBag.LastNonaktifDate = await GetLatestNonaktifDateAsync(header.NoNik, cancellationToken);

            return View(header);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KaryawanCreateViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            if (await IsNikBlacklistedAsync(model.NoNik, cancellationToken) && !IsOwner())
            {
                ModelState.AddModelError(nameof(model.NoNik), "NIK terdeteksi blacklist dan tidak bisa diaktifkan.");
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            if (model.NonaktifEnabled && string.IsNullOrWhiteSpace(model.NonaktifAlasan))
            {
                ModelState.AddModelError(nameof(model.NonaktifAlasan), "Alasan nonaktif wajib diisi.");
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            if (model.BlacklistEnabled && string.IsNullOrWhiteSpace(model.BlacklistAlasan))
            {
                ModelState.AddModelError(nameof(model.BlacklistAlasan), "Alasan blacklist wajib diisi.");
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            var warga = (model.WargaNegara ?? "WNI").Trim().ToUpperInvariant();
            var ktp = (model.NoKtp ?? string.Empty).Trim();
            if (warga == "WNI")
            {
                if (ktp.Length != 16 || !ktp.All(char.IsDigit))
                {
                    ModelState.AddModelError(nameof(model.NoKtp), "No KTP WNI harus 16 digit angka.");
                    await PopulateOptionsAsync(model, cancellationToken);
                    return View(model);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ktp) || !ktp.All(char.IsLetterOrDigit))
                {
                    ModelState.AddModelError(nameof(model.NoKtp), "No paspor WNA harus huruf/angka.");
                    await PopulateOptionsAsync(model, cancellationToken);
                    return View(model);
                }
            }

            var conflictErrors = await GetPersonalConflictErrorsAsync(model.NoKtp, model.NoKk, model.NoNik, null, cancellationToken);
            if (conflictErrors.Count > 0)
            {
                foreach (var error in conflictErrors)
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }

                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            var existsSameCompany = await _context.tbl_t_karyawan.AsNoTracking()
                .AnyAsync(k => k.no_nik == model.NoNik && k.perusahaan_id == model.CompanyId, cancellationToken);
            if (existsSameCompany)
            {
                ModelState.AddModelError(nameof(model.NoNik), "NIK sudah terdaftar di perusahaan ini.");
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            var idKaryawan = await GenerateKaryawanIdAsync(cancellationToken);
            var history = await GetNikHistoryAsync(model.NoNik, model.CompanyId, cancellationToken);
            var lastNonaktif = await GetLatestNonaktifDateAsync(model.NoNik, cancellationToken);
            if (history.LastCompanyId.HasValue && lastNonaktif.HasValue && !IsOwner())
            {
                var diffDays = (DateTime.UtcNow.Date - lastNonaktif.Value.Date).TotalDays;
                if (diffDays < 90)
                {
                    ModelState.AddModelError(nameof(model.NoNik), "Pindah perusahaan minimal 3 bulan dari tanggal nonaktif/resign.");
                    await PopulateOptionsAsync(model, cancellationToken);
                    return View(model);
                }
            }

            if (history.LastCompanyId.HasValue && !lastNonaktif.HasValue && !IsOwner())
            {
                var approved = await HasApprovedMutasiAsync(model.NoNik, history.LastCompanyId.Value, model.CompanyId, cancellationToken);
                if (!approved)
                {
                    ModelState.AddModelError(nameof(model.NoNik), "Mutasi belum disetujui perusahaan asal. Ajukan mutasi terlebih dahulu.");
                    await PopulateOptionsAsync(model, cancellationToken);
                    return View(model);
                }
            }

            var personal = await FindPersonalAsync(model.NoKtp, model.NoKk, cancellationToken);
            if (personal is null)
            {
                personal = new Models.Db.tbl_m_personal
                {
                    no_ktp = model.NoKtp?.Trim(),
                    no_kk = model.NoKk?.Trim(),
                    nama_lengkap = model.NamaLengkap.Trim(),
                    nama_alias = model.NamaAlias?.Trim(),
                    jenis_kelamin = model.JenisKelamin?.Trim(),
                    tempat_lahir = model.TempatLahir?.Trim(),
                    tanggal_lahir = model.TanggalLahir,
                    id_agama = model.IdAgama,
                    id_status_nikah = model.IdStatusNikah,
                    status_nikah = model.StatusNikah?.Trim(),
                    email_pribadi = model.EmailPribadi?.Trim(),
                    hp_1 = model.Hp1?.Trim(),
                    hp_2 = model.Hp2?.Trim(),
                    nama_ibu = model.NamaIbu?.Trim(),
                    nama_ayah = model.NamaAyah?.Trim(),
                    no_npwp = model.NoNpwp?.Trim(),
                    no_bpjs_tk = model.NoBpjsTk?.Trim(),
                    no_bpjs_kes = model.NoBpjsKes?.Trim(),
                    no_bpjs_pensiun = model.NoBpjsPensiun?.Trim(),
                    id_pendidikan = model.IdPendidikan,
                    nama_sekolah = model.NamaSekolah?.Trim(),
                    fakultas = model.Fakultas?.Trim(),
                    jurusan = model.Jurusan?.Trim(),
                    file_pendukung = model.FilePendukung?.Trim(),
                    warga_negara = warga,
                    alamat = model.Alamat?.Trim(),
                    provinsi = model.Provinsi?.Trim(),
                    kabupaten = model.Kabupaten?.Trim(),
                    kecamatan = model.Kecamatan?.Trim(),
                    desa = model.Desa?.Trim(),
                    kode_pos = model.KodePos?.Trim(),
                    created_at = DateTime.UtcNow,
                    created_by = User.Identity?.Name
                };

                _context.tbl_m_personal.Add(personal);
            }
            else
            {
                personal.no_ktp = model.NoKtp?.Trim();
                personal.no_kk = model.NoKk?.Trim();
                personal.nama_lengkap = model.NamaLengkap.Trim();
                personal.nama_alias = model.NamaAlias?.Trim();
                personal.jenis_kelamin = model.JenisKelamin?.Trim();
                personal.tempat_lahir = model.TempatLahir?.Trim();
                personal.tanggal_lahir = model.TanggalLahir;
                personal.id_agama = model.IdAgama;
                personal.id_status_nikah = model.IdStatusNikah;
                personal.status_nikah = model.StatusNikah?.Trim();
                personal.email_pribadi = model.EmailPribadi?.Trim();
                personal.hp_1 = model.Hp1?.Trim();
                personal.hp_2 = model.Hp2?.Trim();
                personal.nama_ibu = model.NamaIbu?.Trim();
                personal.nama_ayah = model.NamaAyah?.Trim();
                personal.no_npwp = model.NoNpwp?.Trim();
                personal.no_bpjs_tk = model.NoBpjsTk?.Trim();
                personal.no_bpjs_kes = model.NoBpjsKes?.Trim();
                personal.no_bpjs_pensiun = model.NoBpjsPensiun?.Trim();
                personal.id_pendidikan = model.IdPendidikan;
                personal.nama_sekolah = model.NamaSekolah?.Trim();
                personal.fakultas = model.Fakultas?.Trim();
                personal.jurusan = model.Jurusan?.Trim();
                personal.file_pendukung = model.FilePendukung?.Trim();
                personal.warga_negara = warga;
                personal.alamat = model.Alamat?.Trim();
                personal.provinsi = model.Provinsi?.Trim();
                personal.kabupaten = model.Kabupaten?.Trim();
                personal.kecamatan = model.Kecamatan?.Trim();
                personal.desa = model.Desa?.Trim();
                personal.kode_pos = model.KodePos?.Trim();
                personal.updated_at = DateTime.UtcNow;
                personal.updated_by = User.Identity?.Name;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var photoPath = await SaveFileAsync(model.FotoFile, "karyawan", cancellationToken);

            var karyawan = new Models.Db.tbl_t_karyawan
            {
                personal_id = personal.personal_id,
                no_nik = model.NoNik.Trim(),
                no_acr = model.NoAcr?.Trim(),
                poh_id = model.IdPoh,
                point_of_hire = model.PointOfHire?.Trim(),
                tanggal_masuk = model.TanggalMasuk,
                tanggal_aktif = model.TanggalAktif,
                email_kantor = model.EmailKantor?.Trim(),
                url_foto = photoPath,
                id_karyawan_indexim = idKaryawan,
                perusahaan_id = model.CompanyId,
                departemen_id = model.DepartmentId,
                seksi_id = model.SectionId,
                jabatan_id = model.PositionId,
                posisi_id = model.PosisiId,
                grade_id = model.IdGrade,
                grade = model.Grade?.Trim(),
                klasifikasi_id = model.IdKlasifikasi,
                klasifikasi = model.Klasifikasi?.Trim(),
                golongan_tipe = model.GolonganTipe?.Trim(),
                roster_id = model.IdRoster,
                roster_kerja = model.RosterKerja?.Trim(),
                status_karyawan_id = model.IdStatusKaryawan,
                paybase_id = model.IdPaybase,
                jenis_pajak_id = model.IdJenisPajak,
                lokasi_penerimaan_id = model.IdLokasiPenerimaan,
                lokasi_penerimaan = model.LokasiPenerimaan?.Trim(),
                lokasi_kerja_id = model.IdLokasiKerja,
                lokasi_kerja = model.LokasiKerja?.Trim(),
                residence_id = model.IdResidence,
                status_residence = model.StatusResidence?.Trim(),
                date_of_hire = model.DateOfHire,
                tanggal_non_aktif = model.TanggalNonAktifKaryawan,
                alasan_non_aktif = model.AlasanNonAktifKaryawan,
                jenis_perjanjian_id = model.IdJenisPerjanjian,
                no_perjanjian = model.NoPerjanjian?.Trim(),
                tanggal_ijin_mulai = model.TanggalIjinMulai,
                tanggal_ijin_akhir = model.TanggalIjinAkhir,
                status_aktif = model.IsActive && !model.NonaktifEnabled && !model.BlacklistEnabled,
                perusahaan_m_id = model.PerusahaanMId,
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name
            };

            _context.tbl_t_karyawan.Add(karyawan);
            await _context.SaveChangesAsync(cancellationToken);

            await SavePendidikanItemsAsync(personal.personal_id, model.PendidikanItems, cancellationToken);

            if (model.DokumenFiles.Count > 0)
            {
                foreach (var file in model.DokumenFiles.Where(f => f is not null && f.Length > 0))
                {
                    var filePath = await SaveFileAsync(file, "karyawan_docs", cancellationToken);
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        continue;
                    }

                    _context.tbl_r_karyawan_dokumen.Add(new Models.Db.tbl_r_karyawan_dokumen
                    {
                        karyawan_id = karyawan.karyawan_id,
                        nama_dokumen = Path.GetFileName(file.FileName),
                        file_path = filePath,
                        created_at = DateTime.UtcNow,
                        created_by = User.Identity?.Name
                    });
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            var penempatan = new Models.Db.tbl_r_karyawan_penempatan
            {
                karyawan_id = karyawan.karyawan_id,
                personal_id = personal.personal_id,
                no_nik = karyawan.no_nik,
                perusahaan_asal_id = history.LastCompanyId,
                perusahaan_tujuan_id = model.CompanyId,
                departemen_id = model.DepartmentId,
                seksi_id = model.SectionId,
                jabatan_id = model.PositionId,
                tanggal_mulai = model.TanggalAktif ?? DateTime.UtcNow.Date,
                status = history.Status,
                sumber_perpindahan = history.Source,
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name
            };

            _context.tbl_r_karyawan_penempatan.Add(penempatan);
            await _context.SaveChangesAsync(cancellationToken);

            if (model.NonaktifEnabled)
            {
                _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
                {
                    karyawan_id = karyawan.karyawan_id,
                    personal_id = personal.personal_id,
                    no_nik = karyawan.no_nik,
                    status = "nonaktif",
                    alasan = model.NonaktifAlasan,
                    kategori_blacklist = model.NonaktifKategori,
                    tanggal_mulai = model.NonaktifTanggal ?? DateTime.UtcNow.Date,
                    created_at = DateTime.UtcNow,
                    created_by = User.Identity?.Name
                });
            }

            if (model.BlacklistEnabled)
            {
                _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
                {
                    karyawan_id = karyawan.karyawan_id,
                    personal_id = personal.personal_id,
                    no_nik = karyawan.no_nik,
                    status = "blacklist",
                    alasan = model.BlacklistAlasan,
                    kategori_blacklist = "blacklist",
                    tanggal_mulai = model.BlacklistTanggal ?? DateTime.UtcNow.Date,
                    created_at = DateTime.UtcNow,
                    created_by = User.Identity?.Name
                });
            }

            if (model.PelanggaranItems.Count > 0)
            {
                foreach (var item in model.PelanggaranItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Jenis) && string.IsNullOrWhiteSpace(item.Keterangan))
                    {
                        continue;
                    }

                    _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
                    {
                        karyawan_id = karyawan.karyawan_id,
                        personal_id = personal.personal_id,
                        no_nik = karyawan.no_nik,
                        status = "pelanggaran",
                        kategori_blacklist = item.Jenis,
                        alasan = item.Keterangan,
                        tanggal_mulai = item.Tanggal ?? DateTime.UtcNow.Date,
                        created_at = DateTime.UtcNow,
                        created_by = User.Identity?.Name
                    });
                }
            }

            if (model.VaksinItems.Count > 0)
            {
                foreach (var item in model.VaksinItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Jenis) && string.IsNullOrWhiteSpace(item.Keterangan))
                    {
                        continue;
                    }

                    _context.tbl_r_karyawan_vaksin.Add(new Models.Db.tbl_r_karyawan_vaksin
                    {
                        karyawan_id = karyawan.karyawan_id,
                        personal_id = personal.personal_id,
                        no_nik = karyawan.no_nik,
                        jenis_vaksin = item.Jenis,
                        dosis = item.Dosis,
                        tanggal_vaksin = item.Tanggal,
                        keterangan = item.Keterangan,
                        created_at = DateTime.UtcNow,
                        created_by = User.Identity?.Name
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            var blacklistStatus = await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => s.no_nik == model.NoNik && (s.status == "blacklist" || s.status == "pelanggaran"))
                .OrderByDescending(s => s.tanggal_mulai)
                .FirstOrDefaultAsync(cancellationToken);

            if (blacklistStatus is not null)
            {
                var notif = new Models.Db.tbl_r_notifikasi_nik
                {
                    no_nik = model.NoNik,
                    karyawan_id = karyawan.karyawan_id,
                    status_terdeteksi = blacklistStatus.status,
                    pesan = $"NIK terdeteksi {blacklistStatus.status}. {blacklistStatus.alasan}",
                    dibuat_pada = DateTime.UtcNow,
                    dibuat_oleh = User.Identity?.Name
                };
                _context.tbl_r_notifikasi_nik.Add(notif);
                await _context.SaveChangesAsync(cancellationToken);

                TempData["AlertMessage"] = "NIK terdeteksi blacklist/pelanggaran. Silakan verifikasi.";
                TempData["AlertType"] = "warning";
            }

            var previousCompanies = await (from k in _context.tbl_t_karyawan.AsNoTracking()
                                            join c in _context.tbl_m_perusahaan.AsNoTracking() on k.perusahaan_id equals c.perusahaan_id
                                            where k.no_nik == model.NoNik && k.perusahaan_id != model.CompanyId
                                            select c.nama_perusahaan)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (previousCompanies.Count > 0 && TempData["AlertMessage"] is null)
            {
                TempData["AlertMessage"] = $"NIK pernah bekerja di: {string.Join(", ", previousCompanies)}.";
                TempData["AlertType"] = "warning";
            }

            await _auditLogger.LogAsync("CREATE", "karyawan", karyawan.karyawan_id.ToString(), $"Tambah karyawan {karyawan.no_nik}", cancellationToken);
            if (TempData["AlertMessage"] is null)
            {
                TempData["AlertMessage"] = "Karyawan berhasil ditambahkan.";
                TempData["AlertType"] = "success";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var canAccess = await ApplyKaryawanScope(_context.tbl_t_karyawan.AsNoTracking()
                    .Where(k => k.karyawan_id == id), scope)
                .AnyAsync(cancellationToken);
            if (!canAccess)
            {
                TempData["AlertMessage"] = "Tidak memiliki akses ke data karyawan ini.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var karyawan = await _context.tbl_t_karyawan.AsNoTracking()
                .FirstOrDefaultAsync(k => k.karyawan_id == id, cancellationToken);
            if (karyawan is null)
            {
                TempData["AlertMessage"] = "Data karyawan tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var personal = await _context.tbl_m_personal.AsNoTracking()
                .FirstOrDefaultAsync(p => p.personal_id == karyawan.personal_id, cancellationToken);
            if (personal is null)
            {
                TempData["AlertMessage"] = "Data personal karyawan tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var latestNonaktif = await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => s.karyawan_id == id && s.status == "nonaktif")
                .OrderByDescending(s => s.tanggal_mulai)
                .FirstOrDefaultAsync(cancellationToken);

            var latestBlacklist = await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => s.karyawan_id == id && s.status == "blacklist")
                .OrderByDescending(s => s.tanggal_mulai)
                .FirstOrDefaultAsync(cancellationToken);

            var model = new KaryawanCreateViewModel
            {
                KaryawanId = karyawan.karyawan_id,
                NoNik = karyawan.no_nik,
                NoAcr = karyawan.no_acr,
                IdKaryawan = karyawan.id_karyawan_indexim,
                WargaNegara = personal.warga_negara,
                NoKtp = personal.no_ktp,
                NoKk = personal.no_kk,
                NamaLengkap = personal.nama_lengkap,
                NamaAlias = personal.nama_alias,
                JenisKelamin = personal.jenis_kelamin,
                TempatLahir = personal.tempat_lahir,
                TanggalLahir = personal.tanggal_lahir,
                IdAgama = personal.id_agama,
                IdStatusNikah = personal.id_status_nikah,
                StatusNikah = personal.status_nikah,
                EmailPribadi = personal.email_pribadi,
                Hp1 = personal.hp_1,
                Hp2 = personal.hp_2,
                NamaIbu = personal.nama_ibu,
                NamaAyah = personal.nama_ayah,
                NoNpwp = personal.no_npwp,
                NoBpjsTk = personal.no_bpjs_tk,
                NoBpjsKes = personal.no_bpjs_kes,
                NoBpjsPensiun = personal.no_bpjs_pensiun,
                IdPendidikan = personal.id_pendidikan,
                NamaSekolah = personal.nama_sekolah,
                Fakultas = personal.fakultas,
                Jurusan = personal.jurusan,
                FilePendukung = personal.file_pendukung,
                Alamat = personal.alamat,
                Provinsi = personal.provinsi,
                Kabupaten = personal.kabupaten,
                Kecamatan = personal.kecamatan,
                Desa = personal.desa,
                KodePos = personal.kode_pos,
                TanggalMasuk = karyawan.tanggal_masuk,
                TanggalAktif = karyawan.tanggal_aktif,
                EmailKantor = karyawan.email_kantor,
                Grade = karyawan.grade,
                IdGrade = karyawan.grade_id,
                Klasifikasi = karyawan.klasifikasi,
                IdKlasifikasi = karyawan.klasifikasi_id,
                GolonganTipe = karyawan.golongan_tipe,
                RosterKerja = karyawan.roster_kerja,
                IdRoster = karyawan.roster_id,
                PointOfHire = karyawan.point_of_hire,
                IdPoh = karyawan.poh_id,
                IdStatusKaryawan = karyawan.status_karyawan_id,
                IdPaybase = karyawan.paybase_id,
                IdJenisPajak = karyawan.jenis_pajak_id,
                LokasiPenerimaan = karyawan.lokasi_penerimaan,
                IdLokasiPenerimaan = karyawan.lokasi_penerimaan_id,
                LokasiKerja = karyawan.lokasi_kerja,
                IdLokasiKerja = karyawan.lokasi_kerja_id,
                StatusResidence = karyawan.status_residence,
                IdResidence = karyawan.residence_id,
                DateOfHire = karyawan.date_of_hire,
                CompanyId = karyawan.perusahaan_id,
                DepartmentId = karyawan.departemen_id,
                SectionId = karyawan.seksi_id,
                PositionId = karyawan.jabatan_id,
                PosisiId = karyawan.posisi_id,
                PerusahaanMId = karyawan.perusahaan_m_id,
                IsActive = karyawan.status_aktif,
                TanggalNonAktifKaryawan = karyawan.tanggal_non_aktif,
                AlasanNonAktifKaryawan = karyawan.alasan_non_aktif,
                IdJenisPerjanjian = karyawan.jenis_perjanjian_id,
                NoPerjanjian = karyawan.no_perjanjian,
                TanggalIjinMulai = karyawan.tanggal_ijin_mulai,
                TanggalIjinAkhir = karyawan.tanggal_ijin_akhir,
                NonaktifEnabled = latestNonaktif is not null,
                NonaktifKategori = latestNonaktif?.kategori_blacklist,
                NonaktifAlasan = latestNonaktif?.alasan,
                NonaktifTanggal = latestNonaktif?.tanggal_mulai,
                BlacklistEnabled = latestBlacklist is not null,
                BlacklistAlasan = latestBlacklist?.alasan,
                BlacklistTanggal = latestBlacklist?.tanggal_mulai,
                CurrentPhotoUrl = karyawan.url_foto
            };

            model.PendidikanItems = await _context.tbl_r_karyawan_pendidikan.AsNoTracking()
                .Where(p => p.personal_id == personal.personal_id)
                .OrderBy(p => p.pendidikan_id)
                .Select(p => new KaryawanPendidikanInput
                {
                    IdPendidikan = p.id_pendidikan,
                    NamaSekolah = p.nama_sekolah,
                    Fakultas = p.fakultas,
                    Jurusan = p.jurusan
                })
                .ToListAsync(cancellationToken);

            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KaryawanCreateViewModel model, CancellationToken cancellationToken)
        {
            if (model.KaryawanId != 0)
            {
                id = model.KaryawanId;
            }

            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var canAccess = await ApplyKaryawanScope(_context.tbl_t_karyawan.AsNoTracking()
                    .Where(k => k.karyawan_id == id), scope)
                .AnyAsync(cancellationToken);
            if (!canAccess)
            {
                TempData["AlertMessage"] = "Tidak memiliki akses ke data karyawan ini.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            var karyawan = await _context.tbl_t_karyawan
                .FirstOrDefaultAsync(k => k.karyawan_id == id, cancellationToken);
            if (karyawan is null)
            {
                TempData["AlertMessage"] = "Data karyawan tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var personal = await _context.tbl_m_personal
                .FirstOrDefaultAsync(p => p.personal_id == karyawan.personal_id, cancellationToken);
            if (personal is null)
            {
                TempData["AlertMessage"] = "Data personal karyawan tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            if (await IsNikBlacklistedAsync(karyawan.no_nik, cancellationToken) && !IsOwner())
            {
                ModelState.AddModelError(nameof(model.NoNik), "NIK terdeteksi blacklist dan hanya Owner yang bisa mengubah status.");
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            if (model.NonaktifEnabled && string.IsNullOrWhiteSpace(model.NonaktifAlasan))
            {
                ModelState.AddModelError(nameof(model.NonaktifAlasan), "Alasan nonaktif wajib diisi.");
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            if (model.BlacklistEnabled && string.IsNullOrWhiteSpace(model.BlacklistAlasan))
            {
                ModelState.AddModelError(nameof(model.BlacklistAlasan), "Alasan blacklist wajib diisi.");
                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            var warga = (model.WargaNegara ?? "WNI").Trim().ToUpperInvariant();
            var ktp = (model.NoKtp ?? string.Empty).Trim();
            if (warga == "WNI")
            {
                if (ktp.Length != 16 || !ktp.All(char.IsDigit))
                {
                    ModelState.AddModelError(nameof(model.NoKtp), "No KTP WNI harus 16 digit angka.");
                    await PopulateOptionsAsync(model, cancellationToken);
                    return View(model);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ktp) || !ktp.All(char.IsLetterOrDigit))
                {
                    ModelState.AddModelError(nameof(model.NoKtp), "No paspor WNA harus huruf/angka.");
                    await PopulateOptionsAsync(model, cancellationToken);
                    return View(model);
                }
            }

            var conflictErrors = await GetPersonalConflictErrorsAsync(model.NoKtp, model.NoKk, karyawan.no_nik, personal.personal_id, cancellationToken);
            if (conflictErrors.Count > 0)
            {
                foreach (var error in conflictErrors)
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }

                await PopulateOptionsAsync(model, cancellationToken);
                return View(model);
            }

            var now = DateTime.UtcNow;
            var audits = new List<Models.Db.tbl_r_karyawan_audit>();
            var changedBy = User.Identity?.Name;

            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "warga_negara", personal.warga_negara, warga, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_ktp", personal.no_ktp, model.NoKtp?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_kk", personal.no_kk, model.NoKk?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "nama_lengkap", personal.nama_lengkap, model.NamaLengkap?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "nama_alias", personal.nama_alias, model.NamaAlias?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "jenis_kelamin", personal.jenis_kelamin, model.JenisKelamin?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tempat_lahir", personal.tempat_lahir, model.TempatLahir?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_lahir", personal.tanggal_lahir, model.TanggalLahir, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_agama", personal.id_agama, model.IdAgama, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_status_nikah", personal.id_status_nikah, model.IdStatusNikah, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "status_nikah", personal.status_nikah, model.StatusNikah?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "email_pribadi", personal.email_pribadi, model.EmailPribadi?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "hp_1", personal.hp_1, model.Hp1?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "hp_2", personal.hp_2, model.Hp2?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "nama_ibu", personal.nama_ibu, model.NamaIbu?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "nama_ayah", personal.nama_ayah, model.NamaAyah?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_npwp", personal.no_npwp, model.NoNpwp?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_bpjs_tk", personal.no_bpjs_tk, model.NoBpjsTk?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_bpjs_kes", personal.no_bpjs_kes, model.NoBpjsKes?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_bpjs_pensiun", personal.no_bpjs_pensiun, model.NoBpjsPensiun?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_pendidikan", personal.id_pendidikan, model.IdPendidikan, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "nama_sekolah", personal.nama_sekolah, model.NamaSekolah?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "fakultas", personal.fakultas, model.Fakultas?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "jurusan", personal.jurusan, model.Jurusan?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "file_pendukung", personal.file_pendukung, model.FilePendukung?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "alamat", personal.alamat, model.Alamat?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "provinsi", personal.provinsi, model.Provinsi?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "kabupaten", personal.kabupaten, model.Kabupaten?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "kecamatan", personal.kecamatan, model.Kecamatan?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "desa", personal.desa, model.Desa?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "kode_pos", personal.kode_pos, model.KodePos?.Trim(), changedBy, now, "edit");

            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_masuk", karyawan.tanggal_masuk, model.TanggalMasuk, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_aktif", karyawan.tanggal_aktif, model.TanggalAktif, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "email_kantor", karyawan.email_kantor, model.EmailKantor?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_acr", karyawan.no_acr, model.NoAcr?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_poh", karyawan.poh_id, model.IdPoh, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "perusahaan_id", karyawan.perusahaan_id, model.CompanyId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "departemen_id", karyawan.departemen_id, model.DepartmentId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "seksi_id", karyawan.seksi_id, model.SectionId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "jabatan_id", karyawan.jabatan_id, model.PositionId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_posisi", karyawan.posisi_id, model.PosisiId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "grade", karyawan.grade, model.Grade?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_grade", karyawan.grade_id, model.IdGrade, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "klasifikasi", karyawan.klasifikasi, model.Klasifikasi?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_klasifikasi", karyawan.klasifikasi_id, model.IdKlasifikasi, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "golongan_tipe", karyawan.golongan_tipe, model.GolonganTipe?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "roster_kerja", karyawan.roster_kerja, model.RosterKerja?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_roster", karyawan.roster_id, model.IdRoster, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_status_karyawan", karyawan.status_karyawan_id, model.IdStatusKaryawan, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_paybase", karyawan.paybase_id, model.IdPaybase, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_jenis_pajak", karyawan.jenis_pajak_id, model.IdJenisPajak, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "point_of_hire", karyawan.point_of_hire, model.PointOfHire?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "lokasi_penerimaan", karyawan.lokasi_penerimaan, model.LokasiPenerimaan?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_lokasi_penerimaan", karyawan.lokasi_penerimaan_id, model.IdLokasiPenerimaan, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "lokasi_kerja", karyawan.lokasi_kerja, model.LokasiKerja?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_lokasi_kerja", karyawan.lokasi_kerja_id, model.IdLokasiKerja, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "status_residence", karyawan.status_residence, model.StatusResidence?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_residence", karyawan.residence_id, model.IdResidence, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "date_of_hire", karyawan.date_of_hire, model.DateOfHire, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_non_aktif", karyawan.tanggal_non_aktif, model.TanggalNonAktifKaryawan, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "alasan_non_aktif", karyawan.alasan_non_aktif, model.AlasanNonAktifKaryawan, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_jenis_perjanjian", karyawan.jenis_perjanjian_id, model.IdJenisPerjanjian, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_perjanjian", karyawan.no_perjanjian, model.NoPerjanjian?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_ijin_mulai", karyawan.tanggal_ijin_mulai, model.TanggalIjinMulai, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_ijin_akhir", karyawan.tanggal_ijin_akhir, model.TanggalIjinAkhir, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "id_m_perusahaan", karyawan.perusahaan_m_id, model.PerusahaanMId, changedBy, now, "edit");

            personal.no_ktp = model.NoKtp?.Trim();
            personal.no_kk = model.NoKk?.Trim();
            personal.nama_lengkap = model.NamaLengkap?.Trim() ?? string.Empty;
            personal.nama_alias = model.NamaAlias?.Trim();
            personal.jenis_kelamin = model.JenisKelamin?.Trim();
            personal.tempat_lahir = model.TempatLahir?.Trim();
            personal.tanggal_lahir = model.TanggalLahir;
            personal.id_agama = model.IdAgama;
            personal.id_status_nikah = model.IdStatusNikah;
            personal.status_nikah = model.StatusNikah?.Trim();
            personal.email_pribadi = model.EmailPribadi?.Trim();
            personal.hp_1 = model.Hp1?.Trim();
            personal.hp_2 = model.Hp2?.Trim();
            personal.nama_ibu = model.NamaIbu?.Trim();
            personal.nama_ayah = model.NamaAyah?.Trim();
            personal.no_npwp = model.NoNpwp?.Trim();
            personal.no_bpjs_tk = model.NoBpjsTk?.Trim();
            personal.no_bpjs_kes = model.NoBpjsKes?.Trim();
            personal.no_bpjs_pensiun = model.NoBpjsPensiun?.Trim();
            personal.id_pendidikan = model.IdPendidikan;
            personal.nama_sekolah = model.NamaSekolah?.Trim();
            personal.fakultas = model.Fakultas?.Trim();
            personal.jurusan = model.Jurusan?.Trim();
            personal.file_pendukung = model.FilePendukung?.Trim();
            personal.warga_negara = warga;
            personal.alamat = model.Alamat?.Trim();
            personal.provinsi = model.Provinsi?.Trim();
            personal.kabupaten = model.Kabupaten?.Trim();
            personal.kecamatan = model.Kecamatan?.Trim();
            personal.desa = model.Desa?.Trim();
            personal.kode_pos = model.KodePos?.Trim();
            personal.updated_at = now;
            personal.updated_by = changedBy;

            var oldCompanyId = karyawan.perusahaan_id;
            var oldDepartmentId = karyawan.departemen_id;
            var oldSectionId = karyawan.seksi_id;
            var oldPositionId = karyawan.jabatan_id;
            var oldStatusAktif = karyawan.status_aktif;
            if (oldCompanyId != model.CompanyId && !IsOwner())
            {
                var lastNonaktif = await GetLatestNonaktifDateAsync(karyawan.no_nik, cancellationToken);
                if (lastNonaktif.HasValue)
                {
                    var diffDays = (DateTime.UtcNow.Date - lastNonaktif.Value.Date).TotalDays;
                    if (diffDays < 90)
                    {
                        ModelState.AddModelError(nameof(model.CompanyId), "Pindah perusahaan minimal 3 bulan dari tanggal nonaktif/resign.");
                        await PopulateOptionsAsync(model, cancellationToken);
                        return View(model);
                    }
                }
            }

            karyawan.tanggal_masuk = model.TanggalMasuk;
            karyawan.tanggal_aktif = model.TanggalAktif;
            karyawan.email_kantor = model.EmailKantor?.Trim();
            karyawan.no_acr = model.NoAcr?.Trim();
            karyawan.poh_id = model.IdPoh;
            karyawan.perusahaan_id = model.CompanyId;
            karyawan.departemen_id = model.DepartmentId;
            karyawan.seksi_id = model.SectionId;
            karyawan.jabatan_id = model.PositionId;
            karyawan.posisi_id = model.PosisiId;
            karyawan.grade_id = model.IdGrade;
            karyawan.grade = model.Grade?.Trim();
            karyawan.klasifikasi_id = model.IdKlasifikasi;
            karyawan.klasifikasi = model.Klasifikasi?.Trim();
            karyawan.golongan_tipe = model.GolonganTipe?.Trim();
            karyawan.roster_id = model.IdRoster;
            karyawan.roster_kerja = model.RosterKerja?.Trim();
            karyawan.point_of_hire = model.PointOfHire?.Trim();
            karyawan.status_karyawan_id = model.IdStatusKaryawan;
            karyawan.paybase_id = model.IdPaybase;
            karyawan.jenis_pajak_id = model.IdJenisPajak;
            karyawan.lokasi_penerimaan_id = model.IdLokasiPenerimaan;
            karyawan.lokasi_penerimaan = model.LokasiPenerimaan?.Trim();
            karyawan.lokasi_kerja_id = model.IdLokasiKerja;
            karyawan.lokasi_kerja = model.LokasiKerja?.Trim();
            karyawan.residence_id = model.IdResidence;
            karyawan.status_residence = model.StatusResidence?.Trim();
            karyawan.date_of_hire = model.DateOfHire;
            karyawan.tanggal_non_aktif = model.TanggalNonAktifKaryawan;
            karyawan.alasan_non_aktif = model.AlasanNonAktifKaryawan;
            karyawan.jenis_perjanjian_id = model.IdJenisPerjanjian;
            karyawan.no_perjanjian = model.NoPerjanjian?.Trim();
            karyawan.tanggal_ijin_mulai = model.TanggalIjinMulai;
            karyawan.tanggal_ijin_akhir = model.TanggalIjinAkhir;
            karyawan.status_aktif = model.IsActive && !model.NonaktifEnabled && !model.BlacklistEnabled;
            karyawan.perusahaan_m_id = model.PerusahaanMId;
            karyawan.updated_at = now;
            karyawan.updated_by = changedBy;

            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "status_aktif", oldStatusAktif, karyawan.status_aktif, changedBy, now, "edit");

            if (model.FotoFile is not null && model.FotoFile.Length > 0)
            {
                var newPhoto = await SaveFileAsync(model.FotoFile, "karyawan", cancellationToken);
                TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "url_foto", karyawan.url_foto, newPhoto, changedBy, now, "edit");
                karyawan.url_foto = newPhoto;
            }

            if (model.DokumenFiles.Count > 0)
            {
                foreach (var file in model.DokumenFiles.Where(f => f is not null && f.Length > 0))
                {
                    var filePath = await SaveFileAsync(file, "karyawan_docs", cancellationToken);
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        continue;
                    }

                    _context.tbl_r_karyawan_dokumen.Add(new Models.Db.tbl_r_karyawan_dokumen
                    {
                        karyawan_id = karyawan.karyawan_id,
                        nama_dokumen = Path.GetFileName(file.FileName),
                        file_path = filePath,
                        created_at = now,
                        created_by = changedBy
                    });
                }
            }

            if (oldCompanyId != model.CompanyId ||
                oldDepartmentId != model.DepartmentId ||
                oldSectionId != model.SectionId ||
                oldPositionId != model.PositionId)
            {
                var isCompanyChanged = oldCompanyId != model.CompanyId;
                var historyStatus = "mutasi";
                var historySource = "edit";

                if (isCompanyChanged)
                {
                    var history = await GetNikHistoryAsync(karyawan.no_nik, model.CompanyId, cancellationToken);
                    historyStatus = history.Status;
                    historySource = history.Source;
                }

                _context.tbl_r_karyawan_penempatan.Add(new Models.Db.tbl_r_karyawan_penempatan
                {
                    karyawan_id = karyawan.karyawan_id,
                    personal_id = personal.personal_id,
                    no_nik = karyawan.no_nik,
                    perusahaan_asal_id = oldCompanyId,
                    perusahaan_tujuan_id = model.CompanyId,
                    departemen_id = model.DepartmentId,
                    seksi_id = model.SectionId,
                    jabatan_id = model.PositionId,
                    tanggal_mulai = model.TanggalAktif ?? DateTime.UtcNow.Date,
                    status = historyStatus,
                    sumber_perpindahan = historySource,
                    created_at = now,
                    created_by = changedBy
                });
            }

            if (model.NonaktifEnabled)
            {
                _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
                {
                    karyawan_id = karyawan.karyawan_id,
                    personal_id = personal.personal_id,
                    no_nik = karyawan.no_nik,
                    status = "nonaktif",
                    alasan = model.NonaktifAlasan,
                    kategori_blacklist = model.NonaktifKategori,
                    tanggal_mulai = model.NonaktifTanggal ?? DateTime.UtcNow.Date,
                    created_at = now,
                    created_by = changedBy
                });
            }

            if (model.BlacklistEnabled)
            {
                _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
                {
                    karyawan_id = karyawan.karyawan_id,
                    personal_id = personal.personal_id,
                    no_nik = karyawan.no_nik,
                    status = "blacklist",
                    alasan = model.BlacklistAlasan,
                    kategori_blacklist = "blacklist",
                    tanggal_mulai = model.BlacklistTanggal ?? DateTime.UtcNow.Date,
                    created_at = now,
                    created_by = changedBy
                });
            }

            if (model.PelanggaranItems.Count > 0)
            {
                foreach (var item in model.PelanggaranItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Jenis) && string.IsNullOrWhiteSpace(item.Keterangan))
                    {
                        continue;
                    }

                    _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
                    {
                        karyawan_id = karyawan.karyawan_id,
                        personal_id = personal.personal_id,
                        no_nik = karyawan.no_nik,
                        status = "pelanggaran",
                        kategori_blacklist = item.Jenis,
                        alasan = item.Keterangan,
                        tanggal_mulai = item.Tanggal ?? DateTime.UtcNow.Date,
                        created_at = now,
                        created_by = changedBy
                    });
                }
            }

            if (model.VaksinItems.Count > 0)
            {
                foreach (var item in model.VaksinItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Jenis) && string.IsNullOrWhiteSpace(item.Keterangan))
                    {
                        continue;
                    }

                    _context.tbl_r_karyawan_vaksin.Add(new Models.Db.tbl_r_karyawan_vaksin
                    {
                        karyawan_id = karyawan.karyawan_id,
                        personal_id = personal.personal_id,
                        no_nik = karyawan.no_nik,
                        jenis_vaksin = item.Jenis,
                        dosis = item.Dosis,
                        tanggal_vaksin = item.Tanggal,
                        keterangan = item.Keterangan,
                        created_at = now,
                        created_by = changedBy
                    });
                }
            }

            if (audits.Count > 0)
            {
                _context.tbl_r_karyawan_audit.AddRange(audits);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await SavePendidikanItemsAsync(personal.personal_id, model.PendidikanItems, cancellationToken);
            await _auditLogger.LogAsync("UPDATE", "karyawan", karyawan.karyawan_id.ToString(), $"Edit karyawan {karyawan.no_nik}", cancellationToken);

            TempData["AlertMessage"] = "Data karyawan berhasil diperbarui.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Detail), new { id = karyawan.karyawan_id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearBlacklist(int id, CancellationToken cancellationToken)
        {
            if (!IsOwner())
            {
                TempData["AlertMessage"] = "Hanya Owner yang dapat membuka blacklist.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var blacklist = await _context.tbl_r_karyawan_status
                .Where(s => s.karyawan_id == id && s.status == "blacklist" && s.tanggal_selesai == null)
                .OrderByDescending(s => s.tanggal_mulai)
                .FirstOrDefaultAsync(cancellationToken);

            if (blacklist is null)
            {
                TempData["AlertMessage"] = "Data blacklist aktif tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Detail), new { id });
            }

            blacklist.tanggal_selesai = DateTime.UtcNow;
            blacklist.updated_at = DateTime.UtcNow;
            blacklist.updated_by = User.Identity?.Name;
            _context.tbl_r_karyawan_audit.Add(new Models.Db.tbl_r_karyawan_audit
            {
                karyawan_id = blacklist.karyawan_id,
                personal_id = blacklist.personal_id,
                no_nik = blacklist.no_nik,
                field_name = "blacklist_status",
                old_value = "blacklist",
                new_value = "cleared",
                changed_at = DateTime.UtcNow,
                changed_by = User.Identity?.Name,
                source = "owner_clear"
            });

            await _context.SaveChangesAsync(cancellationToken);

            await _auditLogger.LogAsync("UPDATE", "karyawan", id.ToString(), "Membuka blacklist karyawan", cancellationToken);
            TempData["AlertMessage"] = "Blacklist berhasil dibuka.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Detail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nonaktifkan(
            int id,
            string statusType,
            string kategori,
            string alasan,
            string? pelanggaranJenis,
            DateTime? pelanggaranTanggal,
            string? pelanggaranKeterangan,
            CancellationToken cancellationToken)
        {
            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            var canAccess = await ApplyKaryawanScope(_context.tbl_t_karyawan.AsNoTracking()
                    .Where(k => k.karyawan_id == id), scope)
                .AnyAsync(cancellationToken);
            if (!canAccess)
            {
                TempData["AlertMessage"] = "Tidak memiliki akses ke data karyawan ini.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(alasan))
            {
                TempData["AlertMessage"] = "Alasan wajib diisi untuk menonaktifkan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var karyawan = await _context.tbl_t_karyawan
                .FirstOrDefaultAsync(k => k.karyawan_id == id, cancellationToken);
            if (karyawan is null)
            {
                TempData["AlertMessage"] = "Data karyawan tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            var normalizedStatus = string.Equals(statusType, "blacklist", StringComparison.OrdinalIgnoreCase)
                ? "blacklist"
                : "nonaktif";

            if (normalizedStatus == "blacklist" && !scope.IsOwner)
            {
                TempData["AlertMessage"] = "Hanya Owner yang bisa mengatur blacklist.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            if (normalizedStatus == "blacklist")
            {
                var alreadyBlacklisted = await IsNikBlacklistedAsync(karyawan.no_nik, cancellationToken);
                if (alreadyBlacklisted)
                {
                    TempData["AlertMessage"] = "Karyawan sudah dalam status blacklist.";
                    TempData["AlertType"] = "warning";
                    return RedirectToAction(nameof(Index));
                }
            }

            karyawan.status_aktif = false;
            karyawan.updated_at = DateTime.UtcNow;
            karyawan.updated_by = User.Identity?.Name;

            _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
            {
                karyawan_id = karyawan.karyawan_id,
                personal_id = karyawan.personal_id,
                no_nik = karyawan.no_nik,
                status = normalizedStatus,
                kategori_blacklist = kategori?.Trim(),
                alasan = alasan.Trim(),
                tanggal_mulai = DateTime.UtcNow.Date,
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name
            });

            if (!string.IsNullOrWhiteSpace(pelanggaranJenis) || !string.IsNullOrWhiteSpace(pelanggaranKeterangan))
            {
                _context.tbl_r_karyawan_status.Add(new Models.Db.tbl_r_karyawan_status
                {
                    karyawan_id = karyawan.karyawan_id,
                    personal_id = karyawan.personal_id,
                    no_nik = karyawan.no_nik,
                    status = "pelanggaran",
                    kategori_blacklist = pelanggaranJenis?.Trim(),
                    alasan = pelanggaranKeterangan?.Trim(),
                    tanggal_mulai = pelanggaranTanggal ?? DateTime.UtcNow.Date,
                    created_at = DateTime.UtcNow,
                    created_by = User.Identity?.Name
                });
            }

            _context.tbl_r_karyawan_audit.Add(new Models.Db.tbl_r_karyawan_audit
            {
                karyawan_id = karyawan.karyawan_id,
                personal_id = karyawan.personal_id,
                no_nik = karyawan.no_nik,
                field_name = "status_aktif",
                old_value = "aktif",
                new_value = normalizedStatus,
                changed_at = DateTime.UtcNow,
                changed_by = User.Identity?.Name,
                source = "list_action"
            });

            await _context.SaveChangesAsync(cancellationToken);

            await _auditLogger.LogAsync("UPDATE", "karyawan", karyawan.karyawan_id.ToString(), $"Nonaktifkan {karyawan.no_nik} ({normalizedStatus})", cancellationToken);

            TempData["AlertMessage"] = normalizedStatus == "blacklist"
                ? "Karyawan berhasil diblacklist."
                : "Karyawan berhasil dinonaktifkan.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> DepartmentsByCompany(int companyId, CancellationToken cancellationToken)
        {
            var departments = await _context.tbl_m_departemen.AsNoTracking()
                .Where(d => d.perusahaan_id == companyId && d.is_aktif == true)
                .OrderBy(d => d.nama_departemen)
                .Select(d => new { id = d.departemen_id, name = d.nama_departemen })
                .ToListAsync(cancellationToken);

            return Json(departments);
        }

        [HttpGet]
        public async Task<IActionResult> SectionsByDepartment(int departmentId, CancellationToken cancellationToken)
        {
            var sections = await _context.tbl_m_seksi.AsNoTracking()
                .Where(s => s.departemen_id == departmentId && s.is_aktif == true)
                .OrderBy(s => s.nama_seksi)
                .Select(s => new { id = s.seksi_id, name = s.nama_seksi })
                .ToListAsync(cancellationToken);

            return Json(sections);
        }

        [HttpGet]
        public async Task<IActionResult> PositionsBySection(int sectionId, CancellationToken cancellationToken)
        {
            var positions = await _context.tbl_m_jabatan.AsNoTracking()
                .Where(p => p.seksi_id == sectionId && p.is_aktif == true)
                .OrderBy(p => p.nama_jabatan)
                .Select(p => new { id = p.jabatan_id, name = p.nama_jabatan })
                .ToListAsync(cancellationToken);

            return Json(positions);
        }

        private async Task PopulateOptionsAsync(KaryawanCreateViewModel model, CancellationToken cancellationToken)
        {
            model.CompanyOptions = await _context.tbl_m_perusahaan.AsNoTracking()
                .Where(c => c.is_aktif)
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);

            model.DepartmentOptions = await _context.tbl_m_departemen.AsNoTracking()
                .Where(d => d.is_aktif == true)
                .OrderBy(d => d.nama_departemen)
                .Select(d => new SelectListItem(d.nama_departemen, d.departemen_id.ToString()))
                .ToListAsync(cancellationToken);

            model.SectionOptions = await _context.tbl_m_seksi.AsNoTracking()
                .Where(s => s.is_aktif == true)
                .OrderBy(s => s.nama_seksi)
                .Select(s => new SelectListItem(s.nama_seksi, s.seksi_id.ToString()))
                .ToListAsync(cancellationToken);

            model.PositionOptions = await _context.tbl_m_jabatan.AsNoTracking()
                .Where(p => p.is_aktif == true)
                .OrderBy(p => p.nama_jabatan)
                .Select(p => new SelectListItem(p.nama_jabatan, p.jabatan_id.ToString()))
                .ToListAsync(cancellationToken);

        }

        private static void TrackAuditChange(List<Models.Db.tbl_r_karyawan_audit> audits, int karyawanId, int personalId, string noNik, string fieldName, object? oldValue, object? newValue, string? changedBy, DateTime changedAt, string source)
        {
            var oldText = NormalizeAuditValue(oldValue);
            var newText = NormalizeAuditValue(newValue);
            if (string.Equals(oldText, newText, StringComparison.Ordinal))
            {
                return;
            }

            audits.Add(new Models.Db.tbl_r_karyawan_audit
            {
                karyawan_id = karyawanId,
                personal_id = personalId,
                no_nik = noNik,
                field_name = fieldName,
                old_value = oldText,
                new_value = newText,
                changed_at = changedAt,
                changed_by = changedBy,
                source = source
            });
        }

        private static string? NormalizeAuditValue(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is DateTime date)
            {
                return date.ToString("yyyy-MM-dd");
            }

            if (value is bool boolValue)
            {
                return boolValue ? "true" : "false";
            }

            return value.ToString()?.Trim();
        }

        private bool IsOwner()
        {
            var role = User.FindFirstValue(System.Security.Claims.ClaimTypes.Role);
            return string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<KaryawanAccessScope> BuildKaryawanAccessScopeAsync(CancellationToken cancellationToken)
        {
            var roleId = GetClaimInt("role_id");
            var companyId = GetClaimInt("company_id");
            var departmentId = GetOptionalClaimInt("department_id");
            var sectionId = GetOptionalClaimInt("section_id");
            var positionId = GetOptionalClaimInt("position_id");

            var roleLevel = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);

            return new KaryawanAccessScope
            {
                RoleId = roleId,
                RoleLevel = roleLevel,
                CompanyId = companyId,
                DepartmentId = departmentId,
                SectionId = sectionId,
                PositionId = positionId,
                IsOwner = IsOwner() || roleLevel >= 4
            };
        }

        private static IQueryable<Models.Db.tbl_t_karyawan> ApplyKaryawanScope(IQueryable<Models.Db.tbl_t_karyawan> query, KaryawanAccessScope scope)
        {
            if (scope.IsOwner)
            {
                return query;
            }

            if (scope.PositionId.HasValue && scope.PositionId.Value > 0)
            {
                return query.Where(k => k.jabatan_id == scope.PositionId);
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                return query.Where(k => k.seksi_id == scope.SectionId);
            }

            if (scope.DepartmentId.HasValue && scope.DepartmentId.Value > 0)
            {
                return query.Where(k => k.departemen_id == scope.DepartmentId);
            }

            if (scope.CompanyId > 0)
            {
                return query.Where(k => k.perusahaan_id == scope.CompanyId);
            }

            return query;
        }

        private int GetClaimInt(string claimType)
        {
            var value = User.FindFirstValue(claimType);
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }

        private int? GetOptionalClaimInt(string claimType)
        {
            var value = User.FindFirstValue(claimType);
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private async Task<bool> IsNikBlacklistedAsync(string noNik, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(noNik))
            {
                return false;
            }

            return await _context.tbl_r_karyawan_status.AsNoTracking()
                .AnyAsync(s => s.no_nik == noNik && s.status == "blacklist" && s.tanggal_selesai == null, cancellationToken);
        }

        private async Task<(string Status, string Source, int? LastCompanyId)> GetNikHistoryAsync(string noNik, int companyId, CancellationToken cancellationToken)
        {
            var lastCompany = await _context.tbl_t_karyawan.AsNoTracking()
                .Where(k => k.no_nik == noNik && k.perusahaan_id != companyId)
                .OrderByDescending(k => k.created_at)
                .Select(k => k.perusahaan_id)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastCompany == 0)
            {
                return ("aktif", "rekrut", null);
            }

            var hasNonaktif = await _context.tbl_r_karyawan_status.AsNoTracking()
                .AnyAsync(s => s.no_nik == noNik && s.status == "nonaktif", cancellationToken);

            return hasNonaktif
                ? ("rehire", "rehire", lastCompany)
                : ("kontrak", "kontrak", lastCompany);
        }

        private async Task<DateTime?> GetLatestNonaktifDateAsync(string noNik, CancellationToken cancellationToken)
        {
            return await _context.tbl_r_karyawan_status.AsNoTracking()
                .Where(s => s.no_nik == noNik && s.status == "nonaktif")
                .OrderByDescending(s => s.tanggal_mulai)
                .Select(s => (DateTime?)s.tanggal_mulai)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<bool> HasApprovedMutasiAsync(string noNik, int perusahaanAsalId, int perusahaanTujuanId, CancellationToken cancellationToken)
        {
            return await _context.tbl_r_karyawan_mutasi_request.AsNoTracking()
                .AnyAsync(r => r.no_nik == noNik
                               && r.perusahaan_asal_id == perusahaanAsalId
                               && r.perusahaan_tujuan_id == perusahaanTujuanId
                               && r.status == "approved", cancellationToken);
        }

        private async Task<Models.Db.tbl_m_personal?> FindPersonalAsync(string? noKtp, string? noKk, CancellationToken cancellationToken)
        {
            var ktp = noKtp?.Trim();
            var kk = noKk?.Trim();
            if (string.IsNullOrWhiteSpace(ktp) && string.IsNullOrWhiteSpace(kk))
            {
                return null;
            }

            return await _context.tbl_m_personal
                .FirstOrDefaultAsync(p =>
                        (!string.IsNullOrWhiteSpace(ktp) && p.no_ktp == ktp)
                        || (!string.IsNullOrWhiteSpace(kk) && p.no_kk == kk),
                    cancellationToken);
        }

        private sealed class PersonalConflictInfo
        {
            public int PersonalId { get; set; }
            public string NoNik { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
        }

        private static string? BuildPersonalConflictMessage(string label, IEnumerable<PersonalConflictInfo> conflicts, string? currentNik)
        {
            var items = conflicts
                .Where(c => !string.Equals(c.NoNik, currentNik, StringComparison.OrdinalIgnoreCase))
                .Select(c => $"{c.NoNik} - {c.CompanyName}")
                .Distinct()
                .Take(3)
                .ToList();
            if (items.Count == 0)
            {
                return null;
            }

            return $"{label} sudah dipakai NIK lain: {string.Join(", ", items)}.";
        }

        private async Task<List<PersonalConflictInfo>> GetPersonalConflictsAsync(string? noKtp, string? noKk, int? ignorePersonalId, CancellationToken cancellationToken)
        {
            var ktp = noKtp?.Trim();
            var kk = noKk?.Trim();
            if (string.IsNullOrWhiteSpace(ktp) && string.IsNullOrWhiteSpace(kk))
            {
                return new List<PersonalConflictInfo>();
            }

            var query = from p in _context.tbl_m_personal.AsNoTracking()
                        join k in _context.tbl_t_karyawan.AsNoTracking() on p.personal_id equals k.personal_id
                        join c in _context.tbl_m_perusahaan.AsNoTracking() on k.perusahaan_id equals c.perusahaan_id
                        where (!string.IsNullOrWhiteSpace(ktp) && p.no_ktp == ktp)
                              || (!string.IsNullOrWhiteSpace(kk) && p.no_kk == kk)
                        select new PersonalConflictInfo
                        {
                            PersonalId = p.personal_id,
                            NoNik = k.no_nik,
                            CompanyName = c.nama_perusahaan ?? string.Empty
                        };

            if (ignorePersonalId.HasValue)
            {
                query = query.Where(c => c.PersonalId != ignorePersonalId.Value);
            }

            return await query.ToListAsync(cancellationToken);
        }

        private async Task<List<(string Field, string Message)>> GetPersonalConflictErrorsAsync(string? noKtp, string? noKk, string? noNik, int? ignorePersonalId, CancellationToken cancellationToken)
        {
            var errors = new List<(string Field, string Message)>();

            if (!string.IsNullOrWhiteSpace(noKtp))
            {
                var conflicts = await GetPersonalConflictsAsync(noKtp, null, ignorePersonalId, cancellationToken);
                var message = BuildPersonalConflictMessage("No KTP", conflicts, noNik);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    errors.Add((nameof(KaryawanCreateViewModel.NoKtp), message));
                }
            }

            if (!string.IsNullOrWhiteSpace(noKk))
            {
                var conflicts = await GetPersonalConflictsAsync(null, noKk, ignorePersonalId, cancellationToken);
                var message = BuildPersonalConflictMessage("No KK", conflicts, noNik);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    errors.Add((nameof(KaryawanCreateViewModel.NoKk), message));
                }
            }

            return errors;
        }

        private class KaryawanAccessScope
        {
            public int RoleId { get; set; }
            public int RoleLevel { get; set; }
            public int CompanyId { get; set; }
            public int? DepartmentId { get; set; }
            public int? SectionId { get; set; }
            public int? PositionId { get; set; }
            public bool IsOwner { get; set; }
        }

        private static bool HasPendidikanValue(KaryawanPendidikanInput item)
        {
            return item.IdPendidikan.HasValue
                || !string.IsNullOrWhiteSpace(item.NamaSekolah)
                || !string.IsNullOrWhiteSpace(item.Fakultas)
                || !string.IsNullOrWhiteSpace(item.Jurusan);
        }

        private static bool HasPendidikanValue(KaryawanImportRowViewModel row)
        {
            return row.IdPendidikan.HasValue
                || !string.IsNullOrWhiteSpace(row.NamaSekolah)
                || !string.IsNullOrWhiteSpace(row.Fakultas)
                || !string.IsNullOrWhiteSpace(row.Jurusan);
        }

        private async Task SavePendidikanItemsAsync(int personalId, List<KaryawanPendidikanInput> items, CancellationToken cancellationToken)
        {
            var existing = await _context.tbl_r_karyawan_pendidikan
                .Where(p => p.personal_id == personalId)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
            {
                _context.tbl_r_karyawan_pendidikan.RemoveRange(existing);
            }

            var now = DateTime.UtcNow;
            foreach (var item in items)
            {
                if (!HasPendidikanValue(item))
                {
                    continue;
                }

                _context.tbl_r_karyawan_pendidikan.Add(new Models.Db.tbl_r_karyawan_pendidikan
                {
                    personal_id = personalId,
                    id_pendidikan = item.IdPendidikan,
                    nama_sekolah = item.NamaSekolah?.Trim(),
                    fakultas = item.Fakultas?.Trim(),
                    jurusan = item.Jurusan?.Trim(),
                    created_at = now,
                    created_by = User.Identity?.Name
                });
            }

            if (existing.Count > 0 || items.Any(HasPendidikanValue))
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task SavePendidikanFromRowAsync(int personalId, KaryawanImportRowViewModel row, CancellationToken cancellationToken)
        {
            if (!HasPendidikanValue(row))
            {
                return;
            }

            var exists = await _context.tbl_r_karyawan_pendidikan.AsNoTracking()
                .AnyAsync(p => p.personal_id == personalId
                               && p.id_pendidikan == row.IdPendidikan
                               && p.nama_sekolah == row.NamaSekolah
                               && p.fakultas == row.Fakultas
                               && p.jurusan == row.Jurusan,
                    cancellationToken);
            if (exists)
            {
                return;
            }

            _context.tbl_r_karyawan_pendidikan.Add(new Models.Db.tbl_r_karyawan_pendidikan
            {
                personal_id = personalId,
                id_pendidikan = row.IdPendidikan,
                nama_sekolah = row.NamaSekolah?.Trim(),
                fakultas = row.Fakultas?.Trim(),
                jurusan = row.Jurusan?.Trim(),
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        private enum ImportRowResult
        {
            Inserted,
            Updated,
            Skipped
        }

        private async Task<ImportRowResult> ApplyImportRowAsync(KaryawanImportRowViewModel row, bool existsSameCompany, CancellationToken cancellationToken)
        {
            if (await IsNikBlacklistedAsync(row.NoNik, cancellationToken) && !IsOwner())
            {
                return ImportRowResult.Skipped;
            }

            var personal = await FindPersonalAsync(row.NoKtp, row.NoKk, cancellationToken);

            if (personal is null)
            {
                personal = new Models.Db.tbl_m_personal
                {
                    no_ktp = row.NoKtp?.Trim(),
                    no_kk = row.NoKk?.Trim(),
                    nama_lengkap = row.NamaLengkap.Trim(),
                    nama_alias = row.NamaAlias?.Trim(),
                    jenis_kelamin = row.JenisKelamin?.Trim(),
                    tempat_lahir = row.TempatLahir?.Trim(),
                    tanggal_lahir = row.TanggalLahir,
                    id_agama = row.IdAgama,
                    id_status_nikah = row.IdStatusNikah,
                    status_nikah = row.StatusNikah?.Trim(),
                    email_pribadi = row.EmailPribadi?.Trim(),
                    hp_1 = row.Hp1?.Trim(),
                    hp_2 = row.Hp2?.Trim(),
                    nama_ibu = row.NamaIbu?.Trim(),
                    nama_ayah = row.NamaAyah?.Trim(),
                    no_npwp = row.NoNpwp?.Trim(),
                    no_bpjs_tk = row.NoBpjsTk?.Trim(),
                    no_bpjs_kes = row.NoBpjsKes?.Trim(),
                    no_bpjs_pensiun = row.NoBpjsPensiun?.Trim(),
                    id_pendidikan = row.IdPendidikan,
                    nama_sekolah = row.NamaSekolah?.Trim(),
                    fakultas = row.Fakultas?.Trim(),
                    jurusan = row.Jurusan?.Trim(),
                    file_pendukung = row.FilePendukung?.Trim(),
                    warga_negara = row.WargaNegara,
                    alamat = row.Alamat?.Trim(),
                    provinsi = row.Provinsi?.Trim(),
                    kabupaten = row.Kabupaten?.Trim(),
                    kecamatan = row.Kecamatan?.Trim(),
                    desa = row.Desa?.Trim(),
                    kode_pos = row.KodePos?.Trim(),
                    created_at = DateTime.UtcNow,
                    created_by = User.Identity?.Name
                };
                _context.tbl_m_personal.Add(personal);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                personal.no_ktp = row.NoKtp?.Trim();
                personal.no_kk = row.NoKk?.Trim();
                personal.nama_lengkap = row.NamaLengkap.Trim();
                personal.nama_alias = row.NamaAlias?.Trim();
                personal.jenis_kelamin = row.JenisKelamin?.Trim();
                personal.tempat_lahir = row.TempatLahir?.Trim();
                personal.tanggal_lahir = row.TanggalLahir;
                personal.id_agama = row.IdAgama;
                personal.id_status_nikah = row.IdStatusNikah;
                personal.status_nikah = row.StatusNikah?.Trim();
                personal.email_pribadi = row.EmailPribadi?.Trim();
                personal.hp_1 = row.Hp1?.Trim();
                personal.hp_2 = row.Hp2?.Trim();
                personal.no_npwp = row.NoNpwp?.Trim();
                personal.no_bpjs_tk = row.NoBpjsTk?.Trim();
                personal.no_bpjs_kes = row.NoBpjsKes?.Trim();
                personal.no_bpjs_pensiun = row.NoBpjsPensiun?.Trim();
                personal.nama_ibu = row.NamaIbu?.Trim();
                personal.nama_ayah = row.NamaAyah?.Trim();
                personal.id_pendidikan = row.IdPendidikan;
                personal.nama_sekolah = row.NamaSekolah?.Trim();
                personal.fakultas = row.Fakultas?.Trim();
                personal.jurusan = row.Jurusan?.Trim();
                personal.file_pendukung = row.FilePendukung?.Trim();
                personal.warga_negara = row.WargaNegara;
                personal.alamat = row.Alamat?.Trim();
                personal.provinsi = row.Provinsi?.Trim();
                personal.kabupaten = row.Kabupaten?.Trim();
                personal.kecamatan = row.Kecamatan?.Trim();
                personal.desa = row.Desa?.Trim();
                personal.kode_pos = row.KodePos?.Trim();
                personal.updated_at = DateTime.UtcNow;
                personal.updated_by = User.Identity?.Name;
                await _context.SaveChangesAsync(cancellationToken);
            }

            await SavePendidikanFromRowAsync(personal.personal_id, row, cancellationToken);

            if (existsSameCompany)
            {
                var karyawan = await _context.tbl_t_karyawan
                    .FirstOrDefaultAsync(k => k.no_nik == row.NoNik && k.perusahaan_id == row.CompanyId, cancellationToken);
                if (karyawan is null)
                {
                    return ImportRowResult.Skipped;
                }

                karyawan.departemen_id = row.DepartmentId;
                karyawan.seksi_id = row.SectionId;
                karyawan.jabatan_id = row.PositionId;
                karyawan.posisi_id = row.PosisiId;
                karyawan.no_acr = row.NoAcr?.Trim();
                karyawan.poh_id = row.PohId;
                karyawan.grade_id = row.IdGrade;
                karyawan.grade = row.Grade?.Trim();
                karyawan.klasifikasi_id = row.IdKlasifikasi;
                karyawan.klasifikasi = row.Klasifikasi?.Trim();
                karyawan.golongan_tipe = row.GolonganTipe?.Trim();
                karyawan.roster_id = row.IdRoster;
                karyawan.roster_kerja = row.RosterKerja?.Trim();
                karyawan.point_of_hire = row.PointOfHire?.Trim();
                karyawan.status_karyawan_id = row.IdStatusKaryawan;
                karyawan.paybase_id = row.IdPaybase;
                karyawan.jenis_pajak_id = row.IdJenisPajak;
                karyawan.lokasi_penerimaan_id = row.IdLokasiPenerimaan;
                karyawan.lokasi_penerimaan = row.LokasiPenerimaan?.Trim();
                karyawan.lokasi_kerja_id = row.IdLokasiKerja;
                karyawan.lokasi_kerja = row.LokasiKerja?.Trim();
                karyawan.residence_id = row.IdResidence;
                karyawan.status_residence = row.StatusResidence?.Trim();
                karyawan.date_of_hire = row.DateOfHire;
                karyawan.tanggal_masuk = row.TanggalMasuk;
                karyawan.tanggal_aktif = row.TanggalAktif;
                karyawan.tanggal_non_aktif = row.TanggalNonAktifKaryawan;
                karyawan.alasan_non_aktif = row.AlasanNonAktifKaryawan;
                karyawan.jenis_perjanjian_id = row.IdJenisPerjanjian;
                karyawan.no_perjanjian = row.NoPerjanjian?.Trim();
                karyawan.tanggal_ijin_mulai = row.TanggalIjinMulai;
                karyawan.tanggal_ijin_akhir = row.TanggalIjinAkhir;
                karyawan.email_kantor = row.EmailKantor?.Trim();
                karyawan.status_aktif = row.IsActive;
                karyawan.perusahaan_m_id = row.PerusahaanMId;
                karyawan.updated_at = DateTime.UtcNow;
                karyawan.updated_by = User.Identity?.Name;

                await _context.SaveChangesAsync(cancellationToken);
                return ImportRowResult.Updated;
            }

            var existingKaryawan = await _context.tbl_t_karyawan.AsNoTracking()
                .Where(k => k.no_nik == row.NoNik)
                .OrderByDescending(k => k.created_at)
                .FirstOrDefaultAsync(cancellationToken);
            if (!IsOwner() && existingKaryawan is not null)
            {
                var lastNonaktif = await GetLatestNonaktifDateAsync(row.NoNik, cancellationToken);
                if (lastNonaktif.HasValue)
                {
                    var diffDays = (DateTime.UtcNow.Date - lastNonaktif.Value.Date).TotalDays;
                    if (diffDays < 90)
                    {
                        return ImportRowResult.Skipped;
                    }
                }
                if (!lastNonaktif.HasValue && existingKaryawan.perusahaan_id != row.CompanyId)
                {
                    var approved = await HasApprovedMutasiAsync(row.NoNik, existingKaryawan.perusahaan_id, row.CompanyId, cancellationToken);
                    if (!approved)
                    {
                        return ImportRowResult.Skipped;
                    }
                }
            }
            var idKaryawan = existingKaryawan?.id_karyawan_indexim ?? await GenerateKaryawanIdAsync(cancellationToken);

            var karyawanNew = new Models.Db.tbl_t_karyawan
            {
                personal_id = personal.personal_id,
                no_nik = row.NoNik.Trim(),
                no_acr = row.NoAcr?.Trim(),
                email_kantor = row.EmailKantor?.Trim(),
                id_karyawan_indexim = idKaryawan,
                perusahaan_id = row.CompanyId,
                departemen_id = row.DepartmentId,
                seksi_id = row.SectionId,
                jabatan_id = row.PositionId,
                posisi_id = row.PosisiId,
                poh_id = row.PohId,
                grade_id = row.IdGrade,
                grade = row.Grade?.Trim(),
                klasifikasi_id = row.IdKlasifikasi,
                klasifikasi = row.Klasifikasi?.Trim(),
                golongan_tipe = row.GolonganTipe?.Trim(),
                roster_id = row.IdRoster,
                roster_kerja = row.RosterKerja?.Trim(),
                point_of_hire = row.PointOfHire?.Trim(),
                status_karyawan_id = row.IdStatusKaryawan,
                paybase_id = row.IdPaybase,
                jenis_pajak_id = row.IdJenisPajak,
                lokasi_penerimaan_id = row.IdLokasiPenerimaan,
                lokasi_penerimaan = row.LokasiPenerimaan?.Trim(),
                lokasi_kerja_id = row.IdLokasiKerja,
                lokasi_kerja = row.LokasiKerja?.Trim(),
                residence_id = row.IdResidence,
                status_residence = row.StatusResidence?.Trim(),
                date_of_hire = row.DateOfHire,
                tanggal_masuk = row.TanggalMasuk,
                tanggal_aktif = row.TanggalAktif,
                tanggal_non_aktif = row.TanggalNonAktifKaryawan,
                alasan_non_aktif = row.AlasanNonAktifKaryawan,
                jenis_perjanjian_id = row.IdJenisPerjanjian,
                no_perjanjian = row.NoPerjanjian?.Trim(),
                tanggal_ijin_mulai = row.TanggalIjinMulai,
                tanggal_ijin_akhir = row.TanggalIjinAkhir,
                status_aktif = row.IsActive,
                perusahaan_m_id = row.PerusahaanMId,
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name
            };

            _context.tbl_t_karyawan.Add(karyawanNew);
            await _context.SaveChangesAsync(cancellationToken);

            var history = await GetNikHistoryAsync(row.NoNik, row.CompanyId, cancellationToken);
            _context.tbl_r_karyawan_penempatan.Add(new Models.Db.tbl_r_karyawan_penempatan
            {
                karyawan_id = karyawanNew.karyawan_id,
                personal_id = personal.personal_id,
                no_nik = karyawanNew.no_nik,
                perusahaan_asal_id = history.LastCompanyId,
                perusahaan_tujuan_id = karyawanNew.perusahaan_id,
                departemen_id = karyawanNew.departemen_id,
                seksi_id = karyawanNew.seksi_id,
                jabatan_id = karyawanNew.jabatan_id,
                tanggal_mulai = karyawanNew.tanggal_aktif ?? DateTime.UtcNow.Date,
                status = history.Status,
                sumber_perpindahan = history.Source,
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name
            });
            await _context.SaveChangesAsync(cancellationToken);

            return ImportRowResult.Inserted;
        }

        private static async Task<string?> SaveFileAsync(Microsoft.AspNetCore.Http.IFormFile? file, string folder, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return null;
            }

            var ext = Path.GetExtension(file.FileName);
            var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
            var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
            Directory.CreateDirectory(root);

            var filePath = Path.Combine(root, safeName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            return $"/uploads/{folder}/{safeName}";
        }

        private async Task<string> GenerateKaryawanIdAsync(CancellationToken cancellationToken)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var code = new char[7];
                for (var i = 0; i < code.Length; i++)
                {
                    code[i] = chars[random.Next(chars.Length)];
                }

                var id = $"IC-{new string(code)}";
                var exists = await _context.tbl_t_karyawan.AsNoTracking()
                    .AnyAsync(k => k.id_karyawan_indexim == id, cancellationToken);
                if (!exists)
                {
                    return id;
                }
            }

            return $"IC-{DateTime.UtcNow:yyMMddHH}";
        }

        private async Task<KaryawanImportPreviewViewModel> BuildImportPreviewAsync(IFormFile file, KaryawanAccessScope scope, CancellationToken cancellationToken)
        {
            using var workbook = new XLWorkbook(file.OpenReadStream());
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null)
            {
                return new KaryawanImportPreviewViewModel();
            }

            var headerRow = sheet.Row(1);
            var headerMap = headerRow.CellsUsed()
                .ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

            var rows = new List<KaryawanImportRowViewModel>();
            var maxRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowIndex = 2; rowIndex <= maxRow; rowIndex++)
            {
                var row = sheet.Row(rowIndex);
                if (row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString())))
                {
                    continue;
                }

                rows.Add(new KaryawanImportRowViewModel
                {
                    RowNumber = rowIndex,
                    WargaNegara = GetCell(row, headerMap, "WargaNegara"),
                    NoKtp = GetCell(row, headerMap, "NoKtp"),
                    NoNik = GetCell(row, headerMap, "NoNik"),
                    NoAcr = GetCell(row, headerMap, "NoAcr"),
                    NamaLengkap = GetCell(row, headerMap, "NamaLengkap"),
                    NamaAlias = GetCell(row, headerMap, "NamaAlias"),
                    NamaIbu = GetCell(row, headerMap, "NamaIbu"),
                    NamaAyah = GetCell(row, headerMap, "NamaAyah"),
                    JenisKelamin = GetCell(row, headerMap, "JenisKelamin"),
                    TempatLahir = GetCell(row, headerMap, "TempatLahir"),
                    TanggalLahir = GetCellDate(row, headerMap, "TanggalLahir"),
                    IdAgama = GetCellIntNullable(row, headerMap, "IdAgama"),
                    IdStatusNikah = GetCellIntNullable(row, headerMap, "IdStatusNikah"),
                    StatusNikah = GetCell(row, headerMap, "StatusNikah"),
                    NoKk = GetCell(row, headerMap, "NoKk"),
                    EmailPribadi = GetCell(row, headerMap, "EmailPribadi"),
                    Hp1 = GetCell(row, headerMap, "Hp1"),
                    Hp2 = GetCell(row, headerMap, "Hp2"),
                    NoNpwp = GetCell(row, headerMap, "NoNpwp"),
                    NoBpjsTk = GetCell(row, headerMap, "NoBpjsTk"),
                    NoBpjsKes = GetCell(row, headerMap, "NoBpjsKes"),
                    NoBpjsPensiun = GetCell(row, headerMap, "NoBpjsPensiun"),
                    IdPendidikan = GetCellIntNullable(row, headerMap, "IdPendidikan"),
                    NamaSekolah = GetCell(row, headerMap, "NamaSekolah"),
                    Fakultas = GetCell(row, headerMap, "Fakultas"),
                    Jurusan = GetCell(row, headerMap, "Jurusan"),
                    FilePendukung = GetCell(row, headerMap, "FilePendukung"),
                    Alamat = GetCell(row, headerMap, "Alamat"),
                    Provinsi = GetCell(row, headerMap, "Provinsi"),
                    Kabupaten = GetCell(row, headerMap, "Kabupaten"),
                    Kecamatan = GetCell(row, headerMap, "Kecamatan"),
                    Desa = GetCell(row, headerMap, "Desa"),
                    KodePos = GetCell(row, headerMap, "KodePos"),
                    CompanyId = GetCellInt(row, headerMap, "CompanyId"),
                    CompanyName = GetCell(row, headerMap, "CompanyName"),
                    DepartmentId = GetCellIntNullable(row, headerMap, "DepartmentId"),
                    DepartmentName = GetCell(row, headerMap, "DepartmentName"),
                    SectionId = GetCellIntNullable(row, headerMap, "SectionId"),
                    SectionName = GetCell(row, headerMap, "SectionName"),
                    PositionId = GetCellIntNullable(row, headerMap, "PositionId"),
                    PositionName = GetCell(row, headerMap, "PositionName"),
                    PosisiId = GetCellIntNullable(row, headerMap, "PosisiId"),
                    IdGrade = GetCellIntNullable(row, headerMap, "IdGrade"),
                    Grade = GetCell(row, headerMap, "Grade"),
                    IdKlasifikasi = GetCellIntNullable(row, headerMap, "IdKlasifikasi"),
                    Klasifikasi = GetCell(row, headerMap, "Klasifikasi"),
                    IdStatusKaryawan = GetCellIntNullable(row, headerMap, "IdStatusKaryawan"),
                    GolonganTipe = GetCell(row, headerMap, "GolonganTipe"),
                    IdRoster = GetCellIntNullable(row, headerMap, "IdRoster"),
                    RosterKerja = GetCell(row, headerMap, "RosterKerja"),
                    PohId = GetCellIntNullable(row, headerMap, "IdPoh"),
                    PointOfHire = GetCell(row, headerMap, "PointOfHire"),
                    IdPaybase = GetCellIntNullable(row, headerMap, "IdPaybase"),
                    IdJenisPajak = GetCellIntNullable(row, headerMap, "IdJenisPajak"),
                    IdLokasiPenerimaan = GetCellIntNullable(row, headerMap, "IdLokasiPenerimaan"),
                    LokasiPenerimaan = GetCell(row, headerMap, "LokasiPenerimaan"),
                    IdLokasiKerja = GetCellIntNullable(row, headerMap, "IdLokasiKerja"),
                    LokasiKerja = GetCell(row, headerMap, "LokasiKerja"),
                    IdResidence = GetCellIntNullable(row, headerMap, "IdResidence"),
                    StatusResidence = GetCell(row, headerMap, "StatusResidence"),
                    DateOfHire = GetCellDate(row, headerMap, "DateOfHire"),
                    TanggalMasuk = GetCellDate(row, headerMap, "TanggalMasuk"),
                    TanggalAktif = GetCellDate(row, headerMap, "TanggalAktif"),
                    TanggalNonAktifKaryawan = GetCellDate(row, headerMap, "TanggalNonAktifKaryawan"),
                    AlasanNonAktifKaryawan = GetCellDate(row, headerMap, "AlasanNonAktifKaryawan"),
                    IdJenisPerjanjian = GetCellIntNullable(row, headerMap, "IdJenisPerjanjian"),
                    NoPerjanjian = GetCell(row, headerMap, "NoPerjanjian"),
                    TanggalIjinMulai = GetCellDate(row, headerMap, "TanggalIjinMulai"),
                    TanggalIjinAkhir = GetCellDate(row, headerMap, "TanggalIjinAkhir"),
                    EmailKantor = GetCell(row, headerMap, "EmailKantor"),
                    PerusahaanMId = GetCellIntNullable(row, headerMap, "PerusahaanMId"),
                    IsActive = GetCellBool(row, headerMap, "IsActive", true)
                });
            }

            if (rows.Count == 0)
            {
                return new KaryawanImportPreviewViewModel { Rows = rows };
            }

            var nikList = rows.Select(r => r.NoNik).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
            var existingKaryawan = await _context.tbl_t_karyawan.AsNoTracking()
                .Where(k => nikList.Contains(k.no_nik))
                .Select(k => new { k.no_nik, k.perusahaan_id, k.status_aktif, k.created_at })
                .ToListAsync(cancellationToken);

            var companyMap = await _context.tbl_m_perusahaan.AsNoTracking()
                .ToDictionaryAsync(c => c.nama_perusahaan ?? string.Empty, c => c.perusahaan_id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var departments = await _context.tbl_m_departemen.AsNoTracking().ToListAsync(cancellationToken);
            var sections = await _context.tbl_m_seksi.AsNoTracking().ToListAsync(cancellationToken);
            var positions = await _context.tbl_m_jabatan.AsNoTracking().ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                var errors = row.Errors;
                var warga = string.IsNullOrWhiteSpace(row.WargaNegara) ? "WNI" : row.WargaNegara.Trim().ToUpperInvariant();

                row.Action = "Insert";
                if (string.IsNullOrWhiteSpace(row.NoNik))
                {
                    errors.Add("NoNik wajib diisi.");
                }

                if (string.IsNullOrWhiteSpace(row.NamaLengkap))
                {
                    errors.Add("NamaLengkap wajib diisi.");
                }

                if (row.CompanyId <= 0 && !string.IsNullOrWhiteSpace(row.CompanyName))
                {
                    if (companyMap.TryGetValue(row.CompanyName, out var companyId))
                    {
                        row.CompanyId = companyId;
                    }
                }

                if (row.CompanyId <= 0)
                {
                    errors.Add("CompanyId/CompanyName wajib diisi.");
                }

                if (!scope.IsOwner && scope.CompanyId > 0 && row.CompanyId != scope.CompanyId)
                {
                    errors.Add("CompanyId tidak sesuai akses.");
                }

                if (warga == "WNI")
                {
                    if (string.IsNullOrWhiteSpace(row.NoKtp) || row.NoKtp.Length != 16 || !row.NoKtp.All(char.IsDigit))
                    {
                        errors.Add("NoKtp WNI harus 16 digit.");
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(row.NoKtp) || !row.NoKtp.All(char.IsLetterOrDigit))
                    {
                        errors.Add("No paspor WNA harus huruf/angka.");
                    }
                }

                var conflictErrors = await GetPersonalConflictErrorsAsync(row.NoKtp, row.NoKk, row.NoNik, null, cancellationToken);
                foreach (var error in conflictErrors)
                {
                    errors.Add(error.Message);
                }

                if (row.CompanyId > 0 && !string.IsNullOrWhiteSpace(row.NoNik))
                {
                    if (existingKaryawan.Any(k => k.no_nik == row.NoNik && k.perusahaan_id == row.CompanyId))
                    {
                        row.Action = "Update";
                    }
                    else if (existingKaryawan.Any(k => k.no_nik == row.NoNik))
                    {
                        row.Action = "Transfer";
                        if (!scope.IsOwner)
                        {
                            var latest = existingKaryawan
                                .Where(k => k.no_nik == row.NoNik && k.perusahaan_id != row.CompanyId)
                                .OrderByDescending(k => k.created_at)
                                .FirstOrDefault();
                            if (latest?.status_aktif == true)
                            {
                                var approved = await HasApprovedMutasiAsync(row.NoNik, latest.perusahaan_id, row.CompanyId, cancellationToken);
                                if (!approved)
                                {
                                    errors.Add("Mutasi belum disetujui perusahaan asal.");
                                }
                            }
                        }
                    }
                }

                if (row.DepartmentId is null && row.CompanyId > 0 && !string.IsNullOrWhiteSpace(row.DepartmentName))
                {
                    row.DepartmentId = departments.FirstOrDefault(d =>
                        d.perusahaan_id == row.CompanyId && d.nama_departemen == row.DepartmentName)?.departemen_id;
                    if (row.DepartmentId is null)
                    {
                        errors.Add("DepartmentName tidak ditemukan.");
                    }
                }

                if (row.SectionId is null && row.DepartmentId is not null && !string.IsNullOrWhiteSpace(row.SectionName))
                {
                    row.SectionId = sections.FirstOrDefault(s =>
                        s.departemen_id == row.DepartmentId && s.nama_seksi == row.SectionName)?.seksi_id;
                    if (row.SectionId is null)
                    {
                        errors.Add("SectionName tidak ditemukan.");
                    }
                }

                if (row.PositionId is null && row.SectionId is not null && !string.IsNullOrWhiteSpace(row.PositionName))
                {
                    row.PositionId = positions.FirstOrDefault(p =>
                        p.seksi_id == row.SectionId && p.nama_jabatan == row.PositionName)?.jabatan_id;
                    if (row.PositionId is null)
                    {
                        errors.Add("PositionName tidak ditemukan.");
                    }
                }

                row.WargaNegara = warga;
                row.IsValid = errors.Count == 0;
                if (!row.IsValid)
                {
                    row.Action = "Error";
                }
            }

            return new KaryawanImportPreviewViewModel { Rows = rows };
        }

        private async Task<(int inserted, int updated, int skipped, List<string> errors)> ImportRowsAsync(IReadOnlyList<KaryawanImportRowViewModel> rows, CancellationToken cancellationToken)
        {
            var inserted = 0;
            var updated = 0;
            var skipped = 0;
            var errors = new List<string>();

            var scope = await BuildKaryawanAccessScopeAsync(cancellationToken);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.NoNik) || row.CompanyId <= 0)
                {
                    skipped++;
                    continue;
                }

                if (!scope.IsOwner && scope.CompanyId > 0 && row.CompanyId != scope.CompanyId)
                {
                    skipped++;
                    errors.Add($"NIK {row.NoNik}: CompanyId tidak sesuai akses.");
                    continue;
                }

                var conflictErrors = await GetPersonalConflictErrorsAsync(row.NoKtp, row.NoKk, row.NoNik, null, cancellationToken);
                if (conflictErrors.Count > 0)
                {
                    skipped++;
                    errors.Add($"NIK {row.NoNik}: {string.Join(" ", conflictErrors.Select(e => e.Message))}");
                    continue;
                }

                var existsSameCompany = await _context.tbl_t_karyawan.AsNoTracking()
                    .AnyAsync(k => k.no_nik == row.NoNik && k.perusahaan_id == row.CompanyId, cancellationToken);

                var result = await ApplyImportRowAsync(row, existsSameCompany, cancellationToken);
                if (result == ImportRowResult.Inserted)
                {
                    inserted++;
                }
                else if (result == ImportRowResult.Updated)
                {
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }

            return (inserted, updated, skipped, errors);
        }

        private static string GetCell(IXLRow row, IReadOnlyDictionary<string, int> map, string header)
        {
            if (!map.TryGetValue(header, out var col)) return string.Empty;
            return row.Cell(col).GetString().Trim();
        }

        private static int GetCellInt(IXLRow row, IReadOnlyDictionary<string, int> map, string header)
        {
            var value = GetCell(row, map, header);
            return int.TryParse(value, out var result) ? result : 0;
        }

        private static int? GetCellIntNullable(IXLRow row, IReadOnlyDictionary<string, int> map, string header)
        {
            var value = GetCell(row, map, header);
            return int.TryParse(value, out var result) ? result : null;
        }

        private static int? ResolveOptionalId(int? primary, int? fallback)
        {
            return primary ?? fallback;
        }

        private static DateTime? GetCellDate(IXLRow row, IReadOnlyDictionary<string, int> map, string header)
        {
            if (!map.TryGetValue(header, out var col)) return null;
            var cell = row.Cell(col);
            if (cell.TryGetValue<DateTime>(out var date))
            {
                return date;
            }

            var raw = cell.GetString();
            return DateTime.TryParse(raw, out var parsed) ? parsed : null;
        }

        private static bool GetCellBool(IXLRow row, IReadOnlyDictionary<string, int> map, string header, bool defaultValue)
        {
            var value = GetCell(row, map, header);
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            if (bool.TryParse(value, out var parsed)) return parsed;
            if (int.TryParse(value, out var numeric)) return numeric != 0;
            return defaultValue;
        }

        private async Task<int> ResolveCompanyIdAsync(string companyName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(companyName)) return 0;
            var company = await _context.tbl_m_perusahaan.AsNoTracking()
                .FirstOrDefaultAsync(c => c.nama_perusahaan == companyName, cancellationToken);
            return company?.perusahaan_id ?? 0;
        }

        private async Task<int?> ResolveDepartmentIdAsync(int companyId, string departmentName, CancellationToken cancellationToken)
        {
            if (companyId <= 0 || string.IsNullOrWhiteSpace(departmentName)) return null;
            var dept = await _context.tbl_m_departemen.AsNoTracking()
                .FirstOrDefaultAsync(d => d.perusahaan_id == companyId && d.nama_departemen == departmentName, cancellationToken);
            return dept?.departemen_id;
        }

        private async Task<int?> ResolveSectionIdAsync(int companyId, string departmentName, string sectionName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sectionName)) return null;
            var deptId = await ResolveDepartmentIdAsync(companyId, departmentName, cancellationToken);
            if (deptId is null) return null;
            var section = await _context.tbl_m_seksi.AsNoTracking()
                .FirstOrDefaultAsync(s => s.departemen_id == deptId && s.nama_seksi == sectionName, cancellationToken);
            return section?.seksi_id;
        }

        private async Task<int?> ResolvePositionIdAsync(int companyId, string departmentName, string sectionName, string positionName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(positionName)) return null;
            var sectionId = await ResolveSectionIdAsync(companyId, departmentName, sectionName, cancellationToken);
            if (sectionId is null) return null;
            var position = await _context.tbl_m_jabatan.AsNoTracking()
                .FirstOrDefaultAsync(p => p.seksi_id == sectionId && p.nama_jabatan == positionName, cancellationToken);
            return position?.jabatan_id;
        }
    }
}





