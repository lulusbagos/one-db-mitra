using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;

namespace one_db_mitra.Controllers
{
    [Authorize]
    public class NotificationCenterController : Controller
    {
        private readonly OneDbMitraContext _context;

        public NotificationCenterController(OneDbMitraContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var items = await BuildNotificationsAsync(30, cancellationToken);
            return View(new NotificationIndexViewModel
            {
                Items = items
            });
        }

        [HttpGet]
        public async Task<IActionResult> Recent(CancellationToken cancellationToken)
        {
            var items = await BuildNotificationsAsync(8, cancellationToken);
            return Json(new { items });
        }

        private async Task<IReadOnlyList<NotificationItem>> BuildNotificationsAsync(int take, CancellationToken cancellationToken)
        {
            var companyId = GetClaimInt("company_id");
            var roleId = GetClaimInt("role_id");
            var departmentId = GetClaimInt("department_id");

            var roleLevel = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);

            var departmentName = departmentId > 0
                ? await _context.tbl_m_departemen.AsNoTracking()
                    .Where(d => d.departemen_id == departmentId)
                    .Select(d => d.nama_departemen)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var isSafetyDept = !string.IsNullOrWhiteSpace(departmentName)
                && departmentName.IndexOf("safety", StringComparison.OrdinalIgnoreCase) >= 0;
            var isOwnerReviewer = roleLevel >= 4 && (departmentId == 0 || isSafetyDept);

            var approvalsQuery = _context.tbl_r_karyawan_mutasi_request.AsNoTracking()
                .Where(r => r.status == "pending" || r.status == "menunggu" || r.status == "request");

            if (companyId > 0)
            {
                approvalsQuery = approvalsQuery.Where(r => r.perusahaan_asal_id == companyId || r.perusahaan_tujuan_id == companyId);
            }

            var approvals = await approvalsQuery
                .OrderByDescending(r => r.tanggal_pengajuan)
                .Take(take)
                .Select(r => new
                {
                    r.request_id,
                    r.no_nik,
                    r.karyawan_id,
                    r.perusahaan_asal_id,
                    r.perusahaan_tujuan_id,
                    r.tanggal_pengajuan
                })
                .ToListAsync(cancellationToken);

            var companyIds = approvals.SelectMany(a => new[] { a.perusahaan_asal_id, a.perusahaan_tujuan_id }).Distinct().ToList();
            var companyMap = await _context.tbl_m_perusahaan.AsNoTracking()
                .Where(c => companyIds.Contains(c.perusahaan_id))
                .ToDictionaryAsync(c => c.perusahaan_id, c => c.nama_perusahaan ?? "-", cancellationToken);

            var approvalItems = approvals.Select(a =>
            {
                companyMap.TryGetValue(a.perusahaan_asal_id, out var asalName);
                companyMap.TryGetValue(a.perusahaan_tujuan_id, out var tujuanName);
                return new NotificationItem
                {
                    Id = $"mutasi:{a.request_id}",
                    Title = "Pengajuan Mutasi",
                    Message = $"NIK {a.no_nik} dari {(asalName ?? "-")} ke {(tujuanName ?? "-")}.",
                    Type = "warning",
                    Category = "approval",
                    CreatedAt = a.tanggal_pengajuan,
                    DueAt = a.tanggal_pengajuan.AddDays(3),
                    Link = a.karyawan_id > 0 ? $"/KaryawanAdmin/Detail/{a.karyawan_id}" : null
                };
            }).ToList();

            var emailQuery = _context.tbl_m_email_notifikasi.AsNoTracking()
                .Where(e => e.status == null || e.status == "queued" || e.status == "error")
                .OrderByDescending(e => e.created_at)
                .Take(take);

            var emailItems = await emailQuery
                .Select(e => new NotificationItem
                {
                    Id = $"email:{e.id}",
                    Title = e.status == "error" ? "Email Gagal" : "Email Queue",
                    Message = $"{e.email_to} - {e.subject}",
                    Type = e.status == "error" ? "danger" : "info",
                    Category = "email",
                    CreatedAt = e.created_at ?? DateTime.UtcNow
                })
                .ToListAsync(cancellationToken);

            var nikQuery = _context.tbl_r_notifikasi_nik.AsNoTracking();
            if (companyId > 0)
            {
                var scopedKaryawanIds = _context.tbl_t_karyawan.AsNoTracking()
                    .Where(k => k.perusahaan_id == companyId)
                    .Select(k => k.karyawan_id);
                nikQuery = nikQuery.Where(n => n.karyawan_id.HasValue && scopedKaryawanIds.Contains(n.karyawan_id.Value));
            }

            var nikItems = await nikQuery
                .OrderByDescending(n => n.dibuat_pada)
                .Take(take)
                .Select(n => new NotificationItem
                {
                    Id = $"nik:{n.notifikasi_id}",
                    Title = "Notifikasi NIK",
                    Message = n.pesan ?? $"NIK {n.no_nik} terdeteksi {n.status_terdeteksi}.",
                    Type = "danger",
                    Category = "nik",
                    CreatedAt = n.dibuat_pada
                })
                .ToListAsync(cancellationToken);

            var pengajuanQuery = _context.tbl_r_pengajuan_perusahaan.AsNoTracking();
            if (isOwnerReviewer)
            {
                pengajuanQuery = pengajuanQuery.Where(p => p.status_pengajuan == "pengajuan_awal"
                                                          || p.status_pengajuan == "menunggu_dokumen"
                                                          || p.status_pengajuan == "review_akhir"
                                                          || p.status_pengajuan == "perlu_perbaikan");
            }
            else if (companyId > 0)
            {
                pengajuanQuery = pengajuanQuery.Where(p => p.perusahaan_id == companyId);
            }
            else
            {
                pengajuanQuery = pengajuanQuery.Where(p => false);
            }

            var pengajuanItems = await pengajuanQuery
                .OrderByDescending(p => p.created_at)
                .Take(take)
                .Select(p => new NotificationItem
                {
                    Id = $"pengajuan:{p.pengajuan_id}",
                    Title = "Pengajuan Perusahaan",
                    Message = $"{p.nama_perusahaan} - {p.status_pengajuan}",
                    Type = p.status_pengajuan == "pengajuan_awal" ? "warning" :
                        p.status_pengajuan == "perlu_perbaikan" ? "info" :
                        p.status_pengajuan.StartsWith("approved") ? "success" : "secondary",
                    Category = "pengajuan",
                    CreatedAt = p.created_at,
                    DueAt = p.created_at.AddDays(3),
                    Link = $"/CompanyRegistration/Review/{p.pengajuan_id}"
                })
                .ToListAsync(cancellationToken);

            var allItems = approvalItems
                .Concat(emailItems)
                .Concat(nikItems)
                .Concat(pengajuanItems)
                .OrderByDescending(i => i.CreatedAt)
                .Take(take)
                .ToList();

            return allItems;
        }

        private int GetClaimInt(string claimType)
        {
            var value = User.FindFirstValue(claimType);
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }
    }
}
