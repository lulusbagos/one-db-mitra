using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;

namespace one_db_mitra.Controllers
{
    [Authorize]
    public class MutasiAdminController : Controller
    {
        private readonly OneDbMitraContext _context;

        public MutasiAdminController(OneDbMitraContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var companyId = GetClaimInt("company_id");
            var isOwner = IsOwner();
            var departmentId = GetOptionalClaimInt("department_id");
            var roleId = GetClaimInt("role_id");
            var roleLevel = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);
            var maxRoleLevel = await _context.tbl_m_peran.AsNoTracking()
                .MaxAsync(r => r.level_akses, cancellationToken);
            var isCompanyTop = departmentId is null || departmentId == 0 || roleLevel >= maxRoleLevel;

            var query = from r in _context.tbl_r_karyawan_mutasi_request.AsNoTracking()
                        join k in _context.tbl_t_karyawan.AsNoTracking() on r.karyawan_id equals k.karyawan_id
                        join p in _context.tbl_m_personal.AsNoTracking() on r.personal_id equals p.personal_id
                        join asal in _context.tbl_m_perusahaan.AsNoTracking() on r.perusahaan_asal_id equals asal.perusahaan_id
                        join tujuan in _context.tbl_m_perusahaan.AsNoTracking() on r.perusahaan_tujuan_id equals tujuan.perusahaan_id
                        where r.perusahaan_asal_id == companyId || r.perusahaan_tujuan_id == companyId || isOwner
                        orderby r.tanggal_pengajuan descending
                        select new MutasiRequestListItem
                        {
                            RequestId = r.request_id,
                            KaryawanId = r.karyawan_id,
                            NoNik = r.no_nik,
                            NamaLengkap = p.nama_lengkap,
                            CompanyAsal = asal.nama_perusahaan ?? "-",
                            CompanyTujuan = tujuan.nama_perusahaan ?? "-",
                            TanggalPengajuan = r.tanggal_pengajuan,
                            Status = r.status,
                            Catatan = r.catatan,
                            DisetujuiOleh = r.disetujui_oleh,
                            TanggalKeputusan = r.tanggal_keputusan,
                            IsIncoming = r.perusahaan_asal_id == companyId,
                            CanApprove = r.status == "pending"
                                && (isOwner || (r.perusahaan_asal_id == companyId && isCompanyTop))
                        };

            var items = await query.ToListAsync(cancellationToken);
            var model = new MutasiIndexViewModel
            {
                Incoming = items.Where(i => i.IsIncoming).ToList(),
                Outgoing = items.Where(i => !i.IsIncoming).ToList()
            };

            ViewBag.IsOwner = isOwner;
            ViewBag.IsCompanyTop = isCompanyTop;
            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new MutasiCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MutasiCreateViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var companyId = GetClaimInt("company_id");
            var existingKaryawan = await _context.tbl_t_karyawan.AsNoTracking()
                .Where(k => k.no_nik == model.NoNik)
                .OrderByDescending(k => k.created_at)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingKaryawan is null)
            {
                ModelState.AddModelError(nameof(model.NoNik), "NIK tidak ditemukan di perusahaan mana pun.");
                return View(model);
            }

            if (existingKaryawan.perusahaan_id == companyId)
            {
                ModelState.AddModelError(nameof(model.NoNik), "Karyawan sudah terdaftar di perusahaan Anda.");
                return View(model);
            }

            var existingRequest = await _context.tbl_r_karyawan_mutasi_request.AsNoTracking()
                .AnyAsync(r => r.no_nik == model.NoNik
                               && r.perusahaan_tujuan_id == companyId
                               && r.perusahaan_asal_id == existingKaryawan.perusahaan_id
                               && r.status == "pending", cancellationToken);
            if (existingRequest)
            {
                ModelState.AddModelError(nameof(model.NoNik), "Pengajuan mutasi masih menunggu persetujuan.");
                return View(model);
            }

            var personal = await _context.tbl_m_personal.AsNoTracking()
                .FirstOrDefaultAsync(p => p.personal_id == existingKaryawan.personal_id, cancellationToken);

            var request = new Models.Db.tbl_r_karyawan_mutasi_request
            {
                karyawan_id = existingKaryawan.karyawan_id,
                personal_id = existingKaryawan.personal_id,
                no_nik = existingKaryawan.no_nik,
                perusahaan_asal_id = existingKaryawan.perusahaan_id,
                perusahaan_tujuan_id = companyId,
                tanggal_pengajuan = DateTime.UtcNow,
                status = "pending",
                catatan = model.Catatan?.Trim(),
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name
            };

            _context.tbl_r_karyawan_mutasi_request.Add(request);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["AlertMessage"] = "Pengajuan mutasi berhasil dikirim. Menunggu persetujuan perusahaan asal.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? catatan, CancellationToken cancellationToken)
        {
            var companyId = GetClaimInt("company_id");
            var isOwner = IsOwner();
            var departmentId = GetOptionalClaimInt("department_id");
            var roleId = GetClaimInt("role_id");
            var roleLevel = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);
            var maxRoleLevel = await _context.tbl_m_peran.AsNoTracking()
                .MaxAsync(r => r.level_akses, cancellationToken);
            var isCompanyTop = departmentId is null || departmentId == 0 || roleLevel >= maxRoleLevel;

            var request = await _context.tbl_r_karyawan_mutasi_request
                .FirstOrDefaultAsync(r => r.request_id == id, cancellationToken);
            if (request is null)
            {
                TempData["AlertMessage"] = "Permintaan mutasi tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            if (!isOwner && (request.perusahaan_asal_id != companyId || !isCompanyTop))
            {
                TempData["AlertMessage"] = "Tidak memiliki akses untuk menyetujui mutasi ini.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            request.status = "approved";
            request.disetujui_oleh = User.Identity?.Name;
            request.tanggal_keputusan = DateTime.UtcNow;
            request.catatan = string.IsNullOrWhiteSpace(catatan) ? request.catatan : catatan.Trim();
            request.updated_at = DateTime.UtcNow;
            request.updated_by = User.Identity?.Name;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["AlertMessage"] = "Mutasi disetujui. Perusahaan tujuan dapat melanjutkan input karyawan.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? catatan, CancellationToken cancellationToken)
        {
            var companyId = GetClaimInt("company_id");
            var isOwner = IsOwner();
            var departmentId = GetOptionalClaimInt("department_id");
            var roleId = GetClaimInt("role_id");
            var roleLevel = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);
            var maxRoleLevel = await _context.tbl_m_peran.AsNoTracking()
                .MaxAsync(r => r.level_akses, cancellationToken);
            var isCompanyTop = departmentId is null || departmentId == 0 || roleLevel >= maxRoleLevel;

            var request = await _context.tbl_r_karyawan_mutasi_request
                .FirstOrDefaultAsync(r => r.request_id == id, cancellationToken);
            if (request is null)
            {
                TempData["AlertMessage"] = "Permintaan mutasi tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            if (!isOwner && (request.perusahaan_asal_id != companyId || !isCompanyTop))
            {
                TempData["AlertMessage"] = "Tidak memiliki akses untuk menolak mutasi ini.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            request.status = "rejected";
            request.disetujui_oleh = User.Identity?.Name;
            request.tanggal_keputusan = DateTime.UtcNow;
            request.catatan = string.IsNullOrWhiteSpace(catatan) ? request.catatan : catatan.Trim();
            request.updated_at = DateTime.UtcNow;
            request.updated_by = User.Identity?.Name;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["AlertMessage"] = "Mutasi ditolak.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
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

        private bool IsOwner()
        {
            var role = User.FindFirstValue(System.Security.Claims.ClaimTypes.Role);
            return string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase);
        }
    }
}
