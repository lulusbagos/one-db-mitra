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
            var baseQuery = ApplyKaryawanScope(_context.tbl_t_karyawan.AsNoTracking().AsQueryable(), scope);
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(k => k.status_aktif);
            }

            var data = await (from k in baseQuery
                              join p in _context.tbl_m_personal.AsNoTracking() on k.personal_id equals p.personal_id
                              join company in _context.tbl_m_perusahaan.AsNoTracking() on k.perusahaan_id equals company.perusahaan_id
                              join dept in _context.tbl_m_departemen.AsNoTracking() on k.departemen_id equals dept.departemen_id into deptJoin
                              from dept in deptJoin.DefaultIfEmpty()
                              join section in _context.tbl_m_seksi.AsNoTracking() on k.seksi_id equals section.seksi_id into sectionJoin
                              from sec in sectionJoin.DefaultIfEmpty()
                              join position in _context.tbl_m_jabatan.AsNoTracking() on k.jabatan_id equals position.jabatan_id into positionJoin
                              from pos in positionJoin.DefaultIfEmpty()
                              orderby k.karyawan_id descending
                              select new KaryawanListItem
                              {
                                  KaryawanId = k.karyawan_id,
                                  NoNik = k.no_nik,
                                  NamaLengkap = p.nama_lengkap,
                                  Email = p.email_pribadi,
                                  Phone = p.hp_1,
                                  CompanyName = company.nama_perusahaan ?? string.Empty,
                                  DepartmentName = dept != null ? dept.nama_departemen ?? "-" : "-",
                                  SectionName = sec != null ? sec.nama_seksi ?? "-" : "-",
                                  PositionName = pos != null ? pos.nama_jabatan ?? "-" : "-",
                                  PhotoUrl = k.url_foto,
                                  IsActive = k.status_aktif
                              }).ToListAsync(cancellationToken);

            var docCounts = await _context.tbl_r_karyawan_dokumen.AsNoTracking()
                .Join(baseQuery, d => d.karyawan_id, k => k.karyawan_id, (d, k) => d)
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

            ViewBag.ActiveCount = await baseQuery.CountAsync(k => k.status_aktif, cancellationToken);
            ViewBag.NonActiveCount = await baseQuery.CountAsync(k => !k.status_aktif, cancellationToken);
            ViewBag.ActiveOnly = activeOnly;
            ViewBag.IsOwner = IsOwner();
            return View(data);
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
                "NamaLengkap",
                "NamaAlias",
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
                "Grade",
                "Klasifikasi",
                "GolonganTipe",
                "RosterKerja",
                "PointOfHire",
                "LokasiPenerimaan",
                "LokasiKerja",
                "StatusResidence",
                "DateOfHire",
                "TanggalMasuk",
                "TanggalAktif",
                "EmailKantor",
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
                "Nama Contoh",
                "Alias Contoh",
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
                "3 SETARA",
                "TEKNISI",
                "NON STAF",
                "10:2",
                "LOCAL",
                "RING I (KALIORANG/KARANGAN/KAUBUN/SANGKULIRANG)",
                "SANGATTA (IC)",
                "RESIDENT (MESS)",
                "2024-01-15",
                "2024-01-15",
                "2024-01-15",
                "contoh@company.com",
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
                                  p.nama_lengkap,
                                  p.nama_alias,
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
                                  k.grade,
                                  k.klasifikasi,
                                  k.golongan_tipe,
                                  k.roster_kerja,
                                  k.point_of_hire,
                                  k.lokasi_penerimaan,
                                  k.lokasi_kerja,
                                  k.status_residence,
                                  k.date_of_hire,
                                  k.tanggal_masuk,
                                  k.tanggal_aktif,
                                  k.email_kantor,
                                  k.status_aktif
                              }).ToListAsync(cancellationToken);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("KARYAWAN");

            var headers = new[]
            {
                "WargaNegara",
                "NoKtp",
                "NoNik",
                "NamaLengkap",
                "NamaAlias",
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
                "Grade",
                "Klasifikasi",
                "GolonganTipe",
                "RosterKerja",
                "PointOfHire",
                "LokasiPenerimaan",
                "LokasiKerja",
                "StatusResidence",
                "DateOfHire",
                "TanggalMasuk",
                "TanggalAktif",
                "EmailKantor",
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
                sheet.Cell(rowIndex, 4).Value = row.nama_lengkap;
                sheet.Cell(rowIndex, 5).Value = row.nama_alias ?? string.Empty;
                sheet.Cell(rowIndex, 6).Value = row.jenis_kelamin ?? string.Empty;
                sheet.Cell(rowIndex, 7).Value = row.tempat_lahir ?? string.Empty;
                sheet.Cell(rowIndex, 8).Value = row.tanggal_lahir?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 9).Value = row.id_agama?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 10).Value = row.id_status_nikah?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 11).Value = row.status_nikah ?? string.Empty;
                sheet.Cell(rowIndex, 12).Value = row.no_npwp ?? string.Empty;
                sheet.Cell(rowIndex, 13).Value = row.no_bpjs_tk ?? string.Empty;
                sheet.Cell(rowIndex, 14).Value = row.no_bpjs_kes ?? string.Empty;
                sheet.Cell(rowIndex, 15).Value = row.no_bpjs_pensiun ?? string.Empty;
                sheet.Cell(rowIndex, 16).Value = row.pendidikan?.id_pendidikan?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 17).Value = row.pendidikan?.nama_sekolah ?? string.Empty;
                sheet.Cell(rowIndex, 18).Value = row.pendidikan?.fakultas ?? string.Empty;
                sheet.Cell(rowIndex, 19).Value = row.pendidikan?.jurusan ?? string.Empty;
                sheet.Cell(rowIndex, 20).Value = row.no_kk ?? string.Empty;
                sheet.Cell(rowIndex, 21).Value = row.email_pribadi ?? string.Empty;
                sheet.Cell(rowIndex, 22).Value = row.hp_1 ?? string.Empty;
                sheet.Cell(rowIndex, 23).Value = row.hp_2 ?? string.Empty;
                sheet.Cell(rowIndex, 24).Value = row.alamat ?? string.Empty;
                sheet.Cell(rowIndex, 25).Value = row.provinsi ?? string.Empty;
                sheet.Cell(rowIndex, 26).Value = row.kabupaten ?? string.Empty;
                sheet.Cell(rowIndex, 27).Value = row.kecamatan ?? string.Empty;
                sheet.Cell(rowIndex, 28).Value = row.desa ?? string.Empty;
                sheet.Cell(rowIndex, 29).Value = row.kode_pos ?? string.Empty;
                sheet.Cell(rowIndex, 30).Value = row.perusahaan_id;
                sheet.Cell(rowIndex, 31).Value = row.nama_perusahaan ?? string.Empty;
                sheet.Cell(rowIndex, 32).Value = row.departemen_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 33).Value = row.departemen_nama ?? string.Empty;
                sheet.Cell(rowIndex, 34).Value = row.seksi_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 35).Value = row.seksi_nama ?? string.Empty;
                sheet.Cell(rowIndex, 36).Value = row.jabatan_id?.ToString() ?? string.Empty;
                sheet.Cell(rowIndex, 37).Value = row.jabatan_nama ?? string.Empty;
                sheet.Cell(rowIndex, 38).Value = row.grade ?? string.Empty;
                sheet.Cell(rowIndex, 39).Value = row.klasifikasi ?? string.Empty;
                sheet.Cell(rowIndex, 40).Value = row.golongan_tipe ?? string.Empty;
                sheet.Cell(rowIndex, 41).Value = row.roster_kerja ?? string.Empty;
                sheet.Cell(rowIndex, 42).Value = row.point_of_hire ?? string.Empty;
                sheet.Cell(rowIndex, 43).Value = row.lokasi_penerimaan ?? string.Empty;
                sheet.Cell(rowIndex, 44).Value = row.lokasi_kerja ?? string.Empty;
                sheet.Cell(rowIndex, 45).Value = row.status_residence ?? string.Empty;
                sheet.Cell(rowIndex, 46).Value = row.date_of_hire?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 47).Value = row.tanggal_masuk?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 48).Value = row.tanggal_aktif?.ToString("yyyy-MM-dd") ?? string.Empty;
                sheet.Cell(rowIndex, 49).Value = row.email_kantor ?? string.Empty;
                sheet.Cell(rowIndex, 50).Value = row.status_aktif ? "1" : "0";
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
                    NamaLengkap = namaLengkap,
                    NamaAlias = GetCell(row, headerMap, "NamaAlias"),
                    JenisKelamin = GetCell(row, headerMap, "JenisKelamin"),
                    TempatLahir = GetCell(row, headerMap, "TempatLahir"),
                    TanggalLahir = GetCellDate(row, headerMap, "TanggalLahir"),
                    IdAgama = GetCellIntNullable(row, headerMap, "IdAgama"),
                    IdStatusNikah = GetCellIntNullable(row, headerMap, "IdStatusNikah"),
                    StatusNikah = GetCell(row, headerMap, "StatusNikah"),
                    NoKk = GetCell(row, headerMap, "NoKk"),
                    EmailPribadi = GetCell(row, headerMap, "EmailPribadi"),
                    Hp1 = GetCell(row, headerMap, "Hp1"),
                    Hp2 = GetCell(row, headerMap, "Hp2"),                    NoNpwp = GetCell(row, headerMap,                "NoNpwp"),
                    NoBpjsTk = GetCell(row, headerMap, "NoBpjsTk"),
                    NoBpjsKes = GetCell(row, headerMap, "NoBpjsKes"),
                    NoBpjsPensiun = GetCell(row, headerMap, "NoBpjsPensiun"),
                    IdPendidikan = GetCellIntNullable(row, headerMap, "IdPendidikan"),
                    NamaSekolah = GetCell(row, headerMap, "NamaSekolah"),
                    Fakultas = GetCell(row, headerMap, "Fakultas"),
                    Jurusan = GetCell(row, headerMap, "Jurusan"),
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
                    Grade = GetCell(row, headerMap, "Grade"),
                    Klasifikasi = GetCell(row, headerMap, "Klasifikasi"),
                    GolonganTipe = GetCell(row, headerMap, "GolonganTipe"),
                    RosterKerja = GetCell(row, headerMap, "RosterKerja"),
                    PointOfHire = GetCell(row, headerMap, "PointOfHire"),
                    LokasiPenerimaan = GetCell(row, headerMap, "LokasiPenerimaan"),
                    LokasiKerja = GetCell(row, headerMap, "LokasiKerja"),
                    StatusResidence = GetCell(row, headerMap, "StatusResidence"),
                    DateOfHire = GetCellDate(row, headerMap, "DateOfHire"),
                    TanggalMasuk = GetCellDate(row, headerMap, "TanggalMasuk"),
                    TanggalAktif = GetCellDate(row, headerMap, "TanggalAktif"),
                    EmailKantor = GetCell(row, headerMap, "EmailKantor"),
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
                                      IdKaryawanIndexim = k.id_karyawan_indexim,
                                      Grade = k.grade,
                                      Klasifikasi = k.klasifikasi,
                                      GolonganTipe = k.golongan_tipe,
                                      RosterKerja = k.roster_kerja,
                                      PointOfHire = k.point_of_hire,
                                      LokasiPenerimaan = k.lokasi_penerimaan,
                                      LokasiKerja = k.lokasi_kerja,
                                      StatusResidence = k.status_residence,
                                      DateOfHire = k.date_of_hire,
                                      IsActive = k.status_aktif,
                                      TanggalMasuk = k.tanggal_masuk,
                                      TanggalAktif = k.tanggal_aktif,
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
                    no_npwp = model.NoNpwp?.Trim(),
                    no_bpjs_tk = model.NoBpjsTk?.Trim(),
                    no_bpjs_kes = model.NoBpjsKes?.Trim(),
                    no_bpjs_pensiun = model.NoBpjsPensiun?.Trim(),
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
                personal.no_npwp = model.NoNpwp?.Trim();
                personal.no_bpjs_tk = model.NoBpjsTk?.Trim();
                personal.no_bpjs_kes = model.NoBpjsKes?.Trim();
                personal.no_bpjs_pensiun = model.NoBpjsPensiun?.Trim();
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
                tanggal_masuk = model.TanggalMasuk,
                tanggal_aktif = model.TanggalAktif,
                email_kantor = model.EmailKantor?.Trim(),
                url_foto = photoPath,
                id_karyawan_indexim = idKaryawan,
                perusahaan_id = model.CompanyId,
                departemen_id = model.DepartmentId,
                seksi_id = model.SectionId,
                jabatan_id = model.PositionId,
                grade = model.Grade?.Trim(),
                klasifikasi = model.Klasifikasi?.Trim(),
                golongan_tipe = model.GolonganTipe?.Trim(),
                roster_kerja = model.RosterKerja?.Trim(),
                point_of_hire = model.PointOfHire?.Trim(),
                lokasi_penerimaan = model.LokasiPenerimaan?.Trim(),
                lokasi_kerja = model.LokasiKerja?.Trim(),
                status_residence = model.StatusResidence?.Trim(),
                date_of_hire = model.DateOfHire,
                status_aktif = model.IsActive && !model.NonaktifEnabled && !model.BlacklistEnabled,
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
                NoNpwp = personal.no_npwp,
                NoBpjsTk = personal.no_bpjs_tk,
                NoBpjsKes = personal.no_bpjs_kes,
                NoBpjsPensiun = personal.no_bpjs_pensiun,
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
                Klasifikasi = karyawan.klasifikasi,
                GolonganTipe = karyawan.golongan_tipe,
                RosterKerja = karyawan.roster_kerja,
                PointOfHire = karyawan.point_of_hire,
                LokasiPenerimaan = karyawan.lokasi_penerimaan,
                LokasiKerja = karyawan.lokasi_kerja,
                StatusResidence = karyawan.status_residence,
                DateOfHire = karyawan.date_of_hire,
                CompanyId = karyawan.perusahaan_id,
                DepartmentId = karyawan.departemen_id,
                SectionId = karyawan.seksi_id,
                PositionId = karyawan.jabatan_id,
                IsActive = karyawan.status_aktif,
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
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_npwp", personal.no_npwp, model.NoNpwp?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_bpjs_tk", personal.no_bpjs_tk, model.NoBpjsTk?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_bpjs_kes", personal.no_bpjs_kes, model.NoBpjsKes?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "no_bpjs_pensiun", personal.no_bpjs_pensiun, model.NoBpjsPensiun?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "alamat", personal.alamat, model.Alamat?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "provinsi", personal.provinsi, model.Provinsi?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "kabupaten", personal.kabupaten, model.Kabupaten?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "kecamatan", personal.kecamatan, model.Kecamatan?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "desa", personal.desa, model.Desa?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "kode_pos", personal.kode_pos, model.KodePos?.Trim(), changedBy, now, "edit");

            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_masuk", karyawan.tanggal_masuk, model.TanggalMasuk, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "tanggal_aktif", karyawan.tanggal_aktif, model.TanggalAktif, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "email_kantor", karyawan.email_kantor, model.EmailKantor?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "perusahaan_id", karyawan.perusahaan_id, model.CompanyId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "departemen_id", karyawan.departemen_id, model.DepartmentId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "seksi_id", karyawan.seksi_id, model.SectionId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "jabatan_id", karyawan.jabatan_id, model.PositionId, changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "grade", karyawan.grade, model.Grade?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "klasifikasi", karyawan.klasifikasi, model.Klasifikasi?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "golongan_tipe", karyawan.golongan_tipe, model.GolonganTipe?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "roster_kerja", karyawan.roster_kerja, model.RosterKerja?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "point_of_hire", karyawan.point_of_hire, model.PointOfHire?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "lokasi_penerimaan", karyawan.lokasi_penerimaan, model.LokasiPenerimaan?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "lokasi_kerja", karyawan.lokasi_kerja, model.LokasiKerja?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "status_residence", karyawan.status_residence, model.StatusResidence?.Trim(), changedBy, now, "edit");
            TrackAuditChange(audits, karyawan.karyawan_id, personal.personal_id, karyawan.no_nik, "date_of_hire", karyawan.date_of_hire, model.DateOfHire, changedBy, now, "edit");

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
            personal.no_npwp = model.NoNpwp?.Trim();
            personal.no_bpjs_tk = model.NoBpjsTk?.Trim();
            personal.no_bpjs_kes = model.NoBpjsKes?.Trim();
            personal.no_bpjs_pensiun = model.NoBpjsPensiun?.Trim();
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
            karyawan.perusahaan_id = model.CompanyId;
            karyawan.departemen_id = model.DepartmentId;
            karyawan.seksi_id = model.SectionId;
            karyawan.jabatan_id = model.PositionId;
            karyawan.grade = model.Grade?.Trim();
            karyawan.klasifikasi = model.Klasifikasi?.Trim();
            karyawan.golongan_tipe = model.GolonganTipe?.Trim();
            karyawan.roster_kerja = model.RosterKerja?.Trim();
            karyawan.point_of_hire = model.PointOfHire?.Trim();
            karyawan.lokasi_penerimaan = model.LokasiPenerimaan?.Trim();
            karyawan.lokasi_kerja = model.LokasiKerja?.Trim();
            karyawan.status_residence = model.StatusResidence?.Trim();
            karyawan.date_of_hire = model.DateOfHire;
            karyawan.status_aktif = model.IsActive && !model.NonaktifEnabled && !model.BlacklistEnabled;
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
        public async Task<IActionResult> Nonaktifkan(int id, string statusType, string kategori, string alasan, CancellationToken cancellationToken)
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
                .Where(d => d.perusahaan_id == companyId && d.is_aktif)
                .OrderBy(d => d.nama_departemen)
                .Select(d => new { id = d.departemen_id, name = d.nama_departemen })
                .ToListAsync(cancellationToken);

            return Json(departments);
        }

        [HttpGet]
        public async Task<IActionResult> SectionsByDepartment(int departmentId, CancellationToken cancellationToken)
        {
            var sections = await _context.tbl_m_seksi.AsNoTracking()
                .Where(s => s.departemen_id == departmentId && s.is_aktif)
                .OrderBy(s => s.nama_seksi)
                .Select(s => new { id = s.seksi_id, name = s.nama_seksi })
                .ToListAsync(cancellationToken);

            return Json(sections);
        }

        [HttpGet]
        public async Task<IActionResult> PositionsBySection(int sectionId, CancellationToken cancellationToken)
        {
            var positions = await _context.tbl_m_jabatan.AsNoTracking()
                .Where(p => p.seksi_id == sectionId && p.is_aktif)
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
                .Where(d => d.is_aktif)
                .OrderBy(d => d.nama_departemen)
                .Select(d => new SelectListItem(d.nama_departemen, d.departemen_id.ToString()))
                .ToListAsync(cancellationToken);

            model.SectionOptions = await _context.tbl_m_seksi.AsNoTracking()
                .Where(s => s.is_aktif)
                .OrderBy(s => s.nama_seksi)
                .Select(s => new SelectListItem(s.nama_seksi, s.seksi_id.ToString()))
                .ToListAsync(cancellationToken);

            model.PositionOptions = await _context.tbl_m_jabatan.AsNoTracking()
                .Where(p => p.is_aktif)
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
                    no_npwp = row.NoNpwp?.Trim(),
                    no_bpjs_tk = row.NoBpjsTk?.Trim(),
                    no_bpjs_kes = row.NoBpjsKes?.Trim(),
                    no_bpjs_pensiun = row.NoBpjsPensiun?.Trim(),
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
                karyawan.grade = row.Grade?.Trim();
                karyawan.klasifikasi = row.Klasifikasi?.Trim();
                karyawan.golongan_tipe = row.GolonganTipe?.Trim();
                karyawan.roster_kerja = row.RosterKerja?.Trim();
                karyawan.point_of_hire = row.PointOfHire?.Trim();
                karyawan.lokasi_penerimaan = row.LokasiPenerimaan?.Trim();
                karyawan.lokasi_kerja = row.LokasiKerja?.Trim();
                karyawan.status_residence = row.StatusResidence?.Trim();
                karyawan.date_of_hire = row.DateOfHire;
                karyawan.tanggal_masuk = row.TanggalMasuk;
                karyawan.tanggal_aktif = row.TanggalAktif;
                karyawan.email_kantor = row.EmailKantor?.Trim();
                karyawan.status_aktif = row.IsActive;
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
            }
            var idKaryawan = existingKaryawan?.id_karyawan_indexim ?? await GenerateKaryawanIdAsync(cancellationToken);

            var karyawanNew = new Models.Db.tbl_t_karyawan
            {
                personal_id = personal.personal_id,
                no_nik = row.NoNik.Trim(),
                email_kantor = row.EmailKantor?.Trim(),
                id_karyawan_indexim = idKaryawan,
                perusahaan_id = row.CompanyId,
                departemen_id = row.DepartmentId,
                seksi_id = row.SectionId,
                jabatan_id = row.PositionId,
                grade = row.Grade?.Trim(),
                klasifikasi = row.Klasifikasi?.Trim(),
                golongan_tipe = row.GolonganTipe?.Trim(),
                roster_kerja = row.RosterKerja?.Trim(),
                point_of_hire = row.PointOfHire?.Trim(),
                lokasi_penerimaan = row.LokasiPenerimaan?.Trim(),
                lokasi_kerja = row.LokasiKerja?.Trim(),
                status_residence = row.StatusResidence?.Trim(),
                date_of_hire = row.DateOfHire,
                tanggal_masuk = row.TanggalMasuk,
                tanggal_aktif = row.TanggalAktif,
                status_aktif = row.IsActive,
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
                    NamaLengkap = GetCell(row, headerMap, "NamaLengkap"),
                    NamaAlias = GetCell(row, headerMap, "NamaAlias"),
                    JenisKelamin = GetCell(row, headerMap, "JenisKelamin"),
                    TempatLahir = GetCell(row, headerMap, "TempatLahir"),
                    TanggalLahir = GetCellDate(row, headerMap, "TanggalLahir"),
                    IdAgama = GetCellIntNullable(row, headerMap, "IdAgama"),
                    IdStatusNikah = GetCellIntNullable(row, headerMap, "IdStatusNikah"),
                    StatusNikah = GetCell(row, headerMap, "StatusNikah"),
                    NoKk = GetCell(row, headerMap, "NoKk"),
                    EmailPribadi = GetCell(row, headerMap, "EmailPribadi"),
                    Hp1 = GetCell(row, headerMap, "Hp1"),
                    Hp2 = GetCell(row, headerMap, "Hp2"),                    NoNpwp = GetCell(row, headerMap,                "NoNpwp"),
                    NoBpjsTk = GetCell(row, headerMap, "NoBpjsTk"),
                    NoBpjsKes = GetCell(row, headerMap, "NoBpjsKes"),
                    NoBpjsPensiun = GetCell(row, headerMap, "NoBpjsPensiun"),
                    IdPendidikan = GetCellIntNullable(row, headerMap, "IdPendidikan"),
                    NamaSekolah = GetCell(row, headerMap, "NamaSekolah"),
                    Fakultas = GetCell(row, headerMap, "Fakultas"),
                    Jurusan = GetCell(row, headerMap, "Jurusan"),
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
                    Grade = GetCell(row, headerMap, "Grade"),
                    Klasifikasi = GetCell(row, headerMap, "Klasifikasi"),
                    GolonganTipe = GetCell(row, headerMap, "GolonganTipe"),
                    RosterKerja = GetCell(row, headerMap, "RosterKerja"),
                    PointOfHire = GetCell(row, headerMap, "PointOfHire"),
                    LokasiPenerimaan = GetCell(row, headerMap, "LokasiPenerimaan"),
                    LokasiKerja = GetCell(row, headerMap, "LokasiKerja"),
                    StatusResidence = GetCell(row, headerMap, "StatusResidence"),
                    DateOfHire = GetCellDate(row, headerMap, "DateOfHire"),
                    TanggalMasuk = GetCellDate(row, headerMap, "TanggalMasuk"),
                    TanggalAktif = GetCellDate(row, headerMap, "TanggalAktif"),
                    EmailKantor = GetCell(row, headerMap, "EmailKantor"),
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
                .Select(k => new { k.no_nik, k.perusahaan_id })
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





