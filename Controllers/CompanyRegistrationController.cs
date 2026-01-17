using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;
using one_db_mitra.Models.Db;
using one_db_mitra.Hubs;

namespace one_db_mitra.Controllers;

[Authorize]
public class CompanyRegistrationController : Controller
{
    private readonly OneDbMitraContext _context;
    private readonly IHubContext<NotificationsHub> _hubContext;

    public CompanyRegistrationController(OneDbMitraContext context, IHubContext<NotificationsHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        var query = _context.tbl_r_pengajuan_perusahaan.AsNoTracking().AsQueryable();

        if (!scope.IsOwner && scope.CompanyId > 0)
        {
            query = query.Where(p => p.perusahaan_id == scope.CompanyId);
        }

        if (scope.IsOwner)
        {
            await QueueReminderEmailsAsync(cancellationToken);
        }

        var typeLookup = await _context.tbl_m_tipe_perusahaan.AsNoTracking()
            .ToDictionaryAsync(t => t.tipe_perusahaan_id, t => t.nama_tipe ?? "-", cancellationToken);

        var approvedCompanies = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
            .Where(p => p.status_pengajuan == "approved" || p.status_pengajuan == "approved_remark")
            .Select(p => p.perusahaan_id ?? 0)
            .ToListAsync(cancellationToken);
        var approvedSet = new HashSet<int>(approvedCompanies);

        var list = await query
            .OrderByDescending(p => p.created_at)
            .Select(p => new PengajuanPerusahaanListItem
            {
                PengajuanId = p.pengajuan_id,
                KodePengajuan = p.kode_pengajuan,
                NamaPerusahaan = p.nama_perusahaan,
                EmailPerusahaan = p.email_perusahaan,
                TipePerusahaan = p.tipe_perusahaan_id.HasValue && typeLookup.ContainsKey(p.tipe_perusahaan_id.Value)
                    ? typeLookup[p.tipe_perusahaan_id.Value]
                    : "-",
                StatusPengajuan = p.status_pengajuan,
                ReviewerNote = p.reviewer_note,
                CreatedAt = p.created_at,
                IsLegacy = p.is_legacy,
                DokumenBelumLengkap = !p.perusahaan_id.HasValue || !approvedSet.Contains(p.perusahaan_id.Value)
            }).ToListAsync(cancellationToken);

        ViewBag.PendingCount = list.Count(x => x.StatusPengajuan == "pengajuan_awal" || x.StatusPengajuan == "menunggu_dokumen" || x.StatusPengajuan == "review_akhir");
        ViewBag.NeedFixCount = list.Count(x => x.StatusPengajuan == "perlu_perbaikan");
        ViewBag.ApprovedCount = list.Count(x => x.StatusPengajuan == "approved" || x.StatusPengajuan == "approved_remark");
        ViewBag.RejectedCount = list.Count(x => x.StatusPengajuan == "ditolak");

        ViewBag.IsOwner = scope.IsOwner;
        ViewBag.CanSubmit = scope.CanSubmit;
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        if (!scope.CanSubmit)
        {
            TempData["AlertMessage"] = "Hanya admin perusahaan (tanpa departemen) yang dapat mengajukan perusahaan.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var model = new PengajuanPerusahaanCreateViewModel();
        var auto = await GetAutoCompanyTargetAsync(scope.CompanyId, cancellationToken);
        model.TipePerusahaanId = auto.TargetTypeId;
        model.PerusahaanIndukId = auto.ParentCompanyId;
        ViewBag.LockType = true;
        ViewBag.LockParent = true;
        ViewBag.ParentCompanyName = auto.ParentCompanyName;
        ViewBag.TargetTypeName = auto.TargetTypeName;
        await PopulateOptionsAsync(model, cancellationToken);
        await PopulateDocTypesAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PengajuanPerusahaanCreateViewModel model, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        if (!scope.CanSubmit)
        {
            TempData["AlertMessage"] = "Tidak memiliki akses untuk mengajukan perusahaan.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var auto = await GetAutoCompanyTargetAsync(scope.CompanyId, cancellationToken);
        model.TipePerusahaanId = auto.TargetTypeId;
        model.PerusahaanIndukId = auto.ParentCompanyId;
        ViewBag.LockType = true;
        ViewBag.LockParent = true;
        ViewBag.ParentCompanyName = auto.ParentCompanyName;
        ViewBag.TargetTypeName = auto.TargetTypeName;

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            await PopulateDocTypesAsync(model, cancellationToken);
            return View(model);
        }

        var kode = await GenerateKodeAsync(cancellationToken);
        var entity = new tbl_r_pengajuan_perusahaan
        {
            kode_pengajuan = kode,
            nama_perusahaan = model.NamaPerusahaan.Trim(),
            email_perusahaan = model.EmailPerusahaan.Trim(),
            tipe_perusahaan_id = model.TipePerusahaanId,
            perusahaan_induk_id = model.PerusahaanIndukId,
            alamat_lengkap = model.AlamatLengkap?.Trim(),
            telepon = model.Telepon?.Trim(),
            contact_person = model.ContactPerson?.Trim(),
            nomor_kontrak = model.NomorKontrak?.Trim(),
            durasi_kontrak = model.DurasiKontrak?.Trim(),
            provinsi_name = model.ProvinsiName?.Trim(),
            regency_name = model.RegencyName?.Trim(),
            district_name = model.DistrictName?.Trim(),
            village_name = model.VillageName?.Trim(),
            status_pengajuan = "pengajuan_awal",
            catatan_pengaju = model.CatatanPengaju?.Trim(),
            created_by = User.Identity?.Name ?? "system",
            created_at = DateTime.UtcNow
        };
        _context.tbl_r_pengajuan_perusahaan.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var allDocTypes = await _context.tbl_r_pengajuan_perusahaan_dokumen_tipe.AsNoTracking()
            .Where(d => d.is_aktif)
            .OrderBy(d => d.doc_type_id)
            .ToListAsync(cancellationToken);
        var docTypes = allDocTypes
            .Where(d => IsInitialDocType(d.nama_dokumen))
            .ToList();

        foreach (var doc in docTypes)
        {
            _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.Add(new tbl_r_pengajuan_perusahaan_dokumen_wajib
            {
                pengajuan_id = entity.pengajuan_id,
                doc_type_id = doc.doc_type_id,
                wajib = false,
                status = "initial"
            });
        }

        _context.tbl_r_pengajuan_perusahaan_log.Add(new tbl_r_pengajuan_perusahaan_log
        {
            pengajuan_id = entity.pengajuan_id,
            aktivitas = "Pengajuan awal dibuat",
            performed_by = User.Identity?.Name ?? "system",
            timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync("notify", new
        {
            message = $"Pengajuan baru: {entity.nama_perusahaan}",
            type = "info"
        }, cancellationToken);

        TempData["AlertMessage"] = "Pengajuan berhasil dibuat. Silakan lengkapi dokumen.";
        TempData["AlertType"] = "success";
        return RedirectToAction(nameof(UploadDokumen), new { id = entity.pengajuan_id });
    }

    [HttpGet]
    public async Task<IActionResult> UploadDokumen(int id, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        var pengajuan = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
            .FirstOrDefaultAsync(p => p.pengajuan_id == id, cancellationToken);
        if (pengajuan is null)
        {
            return NotFound();
        }

        if (!scope.IsOwner && scope.CompanyId > 0 && pengajuan.perusahaan_id != scope.CompanyId)
        {
            TempData["AlertMessage"] = "Tidak memiliki akses ke pengajuan ini.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var model = new PengajuanPerusahaanCreateViewModel
        {
            PengajuanId = pengajuan.pengajuan_id,
            NamaPerusahaan = pengajuan.nama_perusahaan,
            EmailPerusahaan = pengajuan.email_perusahaan,
            TipePerusahaanId = pengajuan.tipe_perusahaan_id,
            PerusahaanIndukId = pengajuan.perusahaan_induk_id,
            AlamatLengkap = pengajuan.alamat_lengkap,
            Telepon = pengajuan.telepon,
            ContactPerson = pengajuan.contact_person,
            NomorKontrak = pengajuan.nomor_kontrak,
            DurasiKontrak = pengajuan.durasi_kontrak,
            ProvinsiName = pengajuan.provinsi_name,
            RegencyName = pengajuan.regency_name,
            DistrictName = pengajuan.district_name,
            VillageName = pengajuan.village_name,
            CatatanPengaju = pengajuan.catatan_pengaju
        };

        await PopulateOptionsAsync(model, cancellationToken);
        await PopulateDocListAsync(model, id, pengajuan.status_pengajuan == "pengajuan_awal", cancellationToken);
        ViewBag.IsOwner = scope.IsOwner;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDokumen(int id, PengajuanPerusahaanCreateViewModel model, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        var pengajuan = await _context.tbl_r_pengajuan_perusahaan
            .FirstOrDefaultAsync(p => p.pengajuan_id == id, cancellationToken);
        if (pengajuan is null)
        {
            return NotFound();
        }

        if (!scope.IsOwner && scope.CompanyId > 0 && pengajuan.perusahaan_id != scope.CompanyId)
        {
            TempData["AlertMessage"] = "Tidak memiliki akses ke pengajuan ini.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var reqs = await _context.tbl_r_pengajuan_perusahaan_dokumen_wajib
            .Where(r => r.pengajuan_id == id)
            .ToListAsync(cancellationToken);

        foreach (var item in model.Dokumen)
        {
            if (item.UploadFile == null || item.ReqId == 0)
            {
                continue;
            }

            var path = await SaveFileAsync(item.UploadFile, "company_docs", cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            _context.tbl_r_pengajuan_perusahaan_dokumen.Add(new tbl_r_pengajuan_perusahaan_dokumen
            {
                req_id = item.ReqId,
                nama_file = item.UploadFile.FileName,
                path_file = path,
                uploaded_by = User.Identity?.Name ?? "system",
                uploaded_at = DateTime.UtcNow,
                status = "uploaded"
            });

            var req = reqs.FirstOrDefault(r => r.req_id == item.ReqId);
            if (req != null)
            {
                req.status = "uploaded";
            }
        }

        var requiredReqs = reqs.Where(r => r.wajib).ToList();
        var requiredComplete = requiredReqs.Count == 0
            ? false
            : requiredReqs.All(r => string.Equals(r.status, "uploaded", StringComparison.OrdinalIgnoreCase));

        if (requiredReqs.Count > 0)
        {
            pengajuan.status_pengajuan = requiredComplete ? "review_akhir" : "menunggu_dokumen";
        }

        pengajuan.updated_at = DateTime.UtcNow;
        pengajuan.updated_by = User.Identity?.Name ?? "system";

        _context.tbl_r_pengajuan_perusahaan_log.Add(new tbl_r_pengajuan_perusahaan_log
        {
            pengajuan_id = pengajuan.pengajuan_id,
            aktivitas = "Dokumen diperbarui",
            performed_by = User.Identity?.Name ?? "system",
            timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        TempData["AlertMessage"] = "Dokumen berhasil diunggah.";
        TempData["AlertType"] = "success";
        return RedirectToAction(nameof(UploadDokumen), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        var pengajuan = await _context.tbl_r_pengajuan_perusahaan
            .FirstOrDefaultAsync(p => p.pengajuan_id == id, cancellationToken);
        if (pengajuan is null)
        {
            return NotFound();
        }

        var currentUser = User.Identity?.Name ?? string.Empty;
        if (!scope.CanSubmit || !string.Equals(pengajuan.created_by, currentUser, StringComparison.OrdinalIgnoreCase))
        {
            TempData["AlertMessage"] = "Tidak memiliki akses untuk menghapus pengajuan ini.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        if (pengajuan.status_pengajuan == "approved" || pengajuan.status_pengajuan == "approved_remark")
        {
            TempData["AlertMessage"] = "Pengajuan yang sudah disetujui tidak bisa dihapus.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var reqIds = await _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.AsNoTracking()
            .Where(r => r.pengajuan_id == id)
            .Select(r => r.req_id)
            .ToListAsync(cancellationToken);

        if (reqIds.Count > 0)
        {
            var docs = _context.tbl_r_pengajuan_perusahaan_dokumen.Where(d => reqIds.Contains(d.req_id));
            _context.tbl_r_pengajuan_perusahaan_dokumen.RemoveRange(docs);
        }

        _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.RemoveRange(
            _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.Where(r => r.pengajuan_id == id));
        _context.tbl_r_pengajuan_perusahaan_review.RemoveRange(
            _context.tbl_r_pengajuan_perusahaan_review.Where(r => r.pengajuan_id == id));
        _context.tbl_r_pengajuan_perusahaan_log.RemoveRange(
            _context.tbl_r_pengajuan_perusahaan_log.Where(r => r.pengajuan_id == id));
        _context.tbl_r_pengajuan_perusahaan_link.RemoveRange(
            _context.tbl_r_pengajuan_perusahaan_link.Where(r => r.pengajuan_id == id));

        _context.tbl_r_pengajuan_perusahaan.Remove(pengajuan);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["AlertMessage"] = "Pengajuan berhasil dihapus.";
        TempData["AlertType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Review(int id, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        if (!scope.IsOwner)
        {
            TempData["AlertMessage"] = "Hanya Safety Owner yang dapat melakukan review.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var pengajuan = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
            .FirstOrDefaultAsync(p => p.pengajuan_id == id, cancellationToken);
        if (pengajuan is null)
        {
            return NotFound();
        }

        var model = await BuildReviewViewModelAsync(pengajuan, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, string actionType, PengajuanReviewViewModel model, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        if (!scope.IsOwner)
        {
            TempData["AlertMessage"] = "Hanya Safety Owner yang dapat melakukan review.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var pengajuan = await _context.tbl_r_pengajuan_perusahaan
            .FirstOrDefaultAsync(p => p.pengajuan_id == id, cancellationToken);
        if (pengajuan is null)
        {
            return NotFound();
        }

        var status = actionType switch
        {
            "approve" => "approved",
            "reject" => "ditolak",
            "rekomendasi" => "menunggu_dokumen",
            _ => "perlu_perbaikan"
        };

        if (status == "approved" && model.ApprovedWithRemark && string.IsNullOrWhiteSpace(model.RemarkApprove))
        {
            ModelState.AddModelError(nameof(model.RemarkApprove), "Remark approve wajib diisi.");
        }

        if (!ModelState.IsValid)
        {
            var retry = await BuildReviewViewModelAsync(pengajuan, cancellationToken);
            retry.RiskCategory = model.RiskCategory;
            retry.CatatanReview = model.CatatanReview;
            retry.ApprovedWithRemark = model.ApprovedWithRemark;
            retry.RemarkApprove = model.RemarkApprove;
            retry.BuktiApproveUrl = model.BuktiApproveUrl;
            retry.TipePerusahaanId = model.TipePerusahaanId;
            return View(retry);
        }

        var reqs = await _context.tbl_r_pengajuan_perusahaan_dokumen_wajib
            .Where(r => r.pengajuan_id == pengajuan.pengajuan_id)
            .ToListAsync(cancellationToken);

        if (model.Dokumen.Count > 0)
        {
            foreach (var doc in model.Dokumen)
            {
                var req = reqs.FirstOrDefault(r => r.req_id == doc.ReqId);
                if (req == null && doc.SelectedRequired)
                {
                    req = new tbl_r_pengajuan_perusahaan_dokumen_wajib
                    {
                        pengajuan_id = pengajuan.pengajuan_id,
                        doc_type_id = doc.DocTypeId,
                        wajib = true,
                        status = "required"
                    };
                    _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.Add(req);
                    reqs.Add(req);
                }

                if (req == null)
                {
                    continue;
                }

                if (doc.SelectedRequired)
                {
                    req.wajib = true;
                    req.status = string.Equals(req.status, "uploaded", StringComparison.OrdinalIgnoreCase) ? "uploaded" : "required";
                }
                else if (req.status != "initial")
                {
                    req.wajib = false;
                    req.status = "not_selected";
                }
            }
        }

        if (model.TipePerusahaanId.HasValue)
        {
            pengajuan.tipe_perusahaan_id = model.TipePerusahaanId.Value;
        }

        if (status == "approved")
        {
            var requiredReqs = reqs.Where(r => r.wajib).ToList();
            var requiredComplete = requiredReqs.Count == 0
                ? false
                : requiredReqs.All(r => string.Equals(r.status, "uploaded", StringComparison.OrdinalIgnoreCase));

            if (!requiredComplete && !model.ApprovedWithRemark)
            {
                ModelState.AddModelError(nameof(model.ApprovedWithRemark), "Dokumen wajib belum lengkap. Gunakan approve dengan remark.");
                var retry = await BuildReviewViewModelAsync(pengajuan, cancellationToken);
                retry.RiskCategory = model.RiskCategory;
                retry.CatatanReview = model.CatatanReview;
                retry.ApprovedWithRemark = model.ApprovedWithRemark;
                retry.RemarkApprove = model.RemarkApprove;
                retry.BuktiApproveUrl = model.BuktiApproveUrl;
                retry.TipePerusahaanId = model.TipePerusahaanId;
                return View(retry);
            }
        }

        pengajuan.status_pengajuan = status == "approved" && model.ApprovedWithRemark ? "approved_remark" : status;
        pengajuan.reviewer_id = User.Identity?.Name;
        pengajuan.reviewer_note = model.CatatanReview;
        pengajuan.risk_category = model.RiskCategory;
        pengajuan.updated_at = DateTime.UtcNow;
        pengajuan.updated_by = User.Identity?.Name ?? "system";

        _context.tbl_r_pengajuan_perusahaan_review.Add(new tbl_r_pengajuan_perusahaan_review
        {
            pengajuan_id = pengajuan.pengajuan_id,
            reviewer_id = User.Identity?.Name ?? "system",
            reviewer_nik = User.FindFirstValue("nik"),
            risk_category = model.RiskCategory,
            action_from = "safety",
            status_review = status,
            catatan = model.CatatanReview,
            approved_with_remark = model.ApprovedWithRemark,
            remark_approve = model.RemarkApprove,
            bukti_approve_url = model.BuktiApproveUrl,
            action_date = DateTime.UtcNow
        });

        var logText = status switch
        {
            "menunggu_dokumen" => "Rekomendasi dokumen dikirim",
            "perlu_perbaikan" => "Perlu perbaikan data/dokumen",
            "ditolak" => "Pengajuan ditolak",
            "approved" => "Pengajuan disetujui",
            _ => $"Review {status}"
        };

        _context.tbl_r_pengajuan_perusahaan_log.Add(new tbl_r_pengajuan_perusahaan_log
        {
            pengajuan_id = pengajuan.pengajuan_id,
            aktivitas = logText,
            performed_by = User.Identity?.Name ?? "system",
            timestamp = DateTime.UtcNow
        });

        if (status == "approved")
        {
            await EnsureCompanyAndUserAsync(pengajuan, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        TempData["AlertMessage"] = "Review berhasil disimpan.";
        TempData["AlertType"] = "success";
        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateLink(int id, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(cancellationToken);
        if (!scope.IsOwner)
        {
            TempData["AlertMessage"] = "Hanya Safety Owner yang dapat membuat link publik.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var pengajuan = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
            .FirstOrDefaultAsync(p => p.pengajuan_id == id, cancellationToken);
        if (pengajuan is null)
        {
            return NotFound();
        }

        var token = Guid.NewGuid().ToString("N");
        _context.tbl_r_pengajuan_perusahaan_link.Add(new tbl_r_pengajuan_perusahaan_link
        {
            pengajuan_id = id,
            token = token,
            status = "active",
            created_by = User.Identity?.Name ?? "system",
            created_at = DateTime.UtcNow
        });

        _context.tbl_r_pengajuan_perusahaan_log.Add(new tbl_r_pengajuan_perusahaan_log
        {
            pengajuan_id = id,
            aktivitas = "Link publik dibuat",
            performed_by = User.Identity?.Name ?? "system",
            timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        TempData["AlertMessage"] = "Link publik berhasil dibuat.";
        TempData["AlertType"] = "success";
        return RedirectToAction(nameof(Review), new { id });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> PublicLink(string token, CancellationToken cancellationToken)
    {
        var link = await _context.tbl_r_pengajuan_perusahaan_link.AsNoTracking()
            .FirstOrDefaultAsync(l => l.token == token && l.status == "active", cancellationToken);
        if (link is null || (link.expired_at.HasValue && link.expired_at.Value < DateTime.UtcNow))
        {
            return NotFound();
        }

        var pengajuan = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
            .FirstOrDefaultAsync(p => p.pengajuan_id == link.pengajuan_id, cancellationToken);
        if (pengajuan is null)
        {
            return NotFound();
        }

        var model = new PengajuanPublicUploadViewModel
        {
            PengajuanId = pengajuan.pengajuan_id,
            KodePengajuan = pengajuan.kode_pengajuan,
            NamaPerusahaan = pengajuan.nama_perusahaan,
            Token = token
        };

        var docs = await BuildDocItemsAsync(pengajuan.pengajuan_id, false, cancellationToken);
        model.Dokumen.AddRange(docs);
        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublicLink(string token, PengajuanPublicUploadViewModel model, CancellationToken cancellationToken)
    {
        var link = await _context.tbl_r_pengajuan_perusahaan_link
            .FirstOrDefaultAsync(l => l.token == token && l.status == "active", cancellationToken);
        if (link is null)
        {
            return NotFound();
        }

        foreach (var item in model.Dokumen)
        {
            if (item.UploadFile == null || item.ReqId == 0)
            {
                continue;
            }

            var path = await SaveFileAsync(item.UploadFile, "company_docs", cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            _context.tbl_r_pengajuan_perusahaan_dokumen.Add(new tbl_r_pengajuan_perusahaan_dokumen
            {
                req_id = item.ReqId,
                nama_file = item.UploadFile.FileName,
                path_file = path,
                uploaded_by = "public",
                uploaded_at = DateTime.UtcNow,
                status = "uploaded"
            });

            var req = await _context.tbl_r_pengajuan_perusahaan_dokumen_wajib
                .FirstOrDefaultAsync(r => r.req_id == item.ReqId, cancellationToken);
            if (req != null)
            {
                req.status = "uploaded";
            }
        }

        link.used_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        TempData["PublicMessage"] = "Dokumen berhasil diunggah.";
        return RedirectToAction(nameof(PublicLink), new { token });
    }

    private async Task PopulateOptionsAsync(PengajuanPerusahaanCreateViewModel model, CancellationToken cancellationToken)
    {
        model.TipePerusahaanOptions = await _context.tbl_m_tipe_perusahaan.AsNoTracking()
            .OrderBy(t => t.nama_tipe)
            .Select(t => new SelectListItem
            {
                Text = t.nama_tipe ?? "-",
                Value = t.tipe_perusahaan_id.ToString()
            }).ToListAsync(cancellationToken);

        model.PerusahaanIndukOptions = await _context.tbl_m_perusahaan.AsNoTracking()
            .OrderBy(p => p.nama_perusahaan)
            .Select(p => new SelectListItem
            {
                Text = p.nama_perusahaan ?? "-",
                Value = p.perusahaan_id.ToString()
            }).ToListAsync(cancellationToken);
    }

    private async Task PopulateDocTypesAsync(PengajuanPerusahaanCreateViewModel model, CancellationToken cancellationToken)
    {
        var docTypes = await _context.tbl_r_pengajuan_perusahaan_dokumen_tipe.AsNoTracking()
            .Where(d => d.is_aktif)
            .OrderBy(d => d.grup)
            .ThenBy(d => d.nama_dokumen)
            .ToListAsync(cancellationToken);

        model.Dokumen = docTypes.Select(doc => new PengajuanDokumenItem
        {
            DocTypeId = doc.doc_type_id,
            Grup = doc.grup,
            NamaDokumen = doc.nama_dokumen,
            Deskripsi = doc.deskripsi,
            Wajib = true
        }).ToList();
    }

    private async Task PopulateDocListAsync(PengajuanPerusahaanCreateViewModel model, int pengajuanId, bool initialOnly, CancellationToken cancellationToken)
    {
        var docs = await BuildDocItemsAsync(pengajuanId, initialOnly, cancellationToken);
        model.Dokumen = docs;
    }

    private async Task<List<PengajuanDokumenItem>> BuildDocItemsAsync(int pengajuanId, bool initialOnly, CancellationToken cancellationToken)
    {
        var reqs = await (from req in _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.AsNoTracking()
                          join doc in _context.tbl_r_pengajuan_perusahaan_dokumen_tipe.AsNoTracking()
                              on req.doc_type_id equals doc.doc_type_id
                          where req.pengajuan_id == pengajuanId
                                && (initialOnly ? req.status == "initial" : req.wajib)
                          orderby doc.grup, doc.nama_dokumen
                          select new PengajuanDokumenItem
                          {
                              ReqId = req.req_id,
                              DocTypeId = doc.doc_type_id,
                              Grup = doc.grup,
                              NamaDokumen = doc.nama_dokumen,
                              Deskripsi = doc.deskripsi,
                              Wajib = req.wajib,
                              Status = req.status,
                              IsRequired = req.wajib,
                              SelectedRequired = req.wajib
                          }).ToListAsync(cancellationToken);

        var files = await _context.tbl_r_pengajuan_perusahaan_dokumen.AsNoTracking()
            .Where(d => reqs.Select(r => r.ReqId).Contains(d.req_id))
            .ToListAsync(cancellationToken);

        foreach (var item in reqs)
        {
            var file = files.LastOrDefault(f => f.req_id == item.ReqId);
            if (file != null)
            {
                item.FileUrl = file.path_file;
                item.Catatan = file.catatan;
                item.Status = file.status;
            }
        }

        return reqs;
    }

    private async Task<List<PengajuanDokumenItem>> BuildReviewDocItemsAsync(int pengajuanId, CancellationToken cancellationToken)
    {
        var reqs = await _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.AsNoTracking()
            .Where(r => r.pengajuan_id == pengajuanId)
            .ToListAsync(cancellationToken);

        var docs = await _context.tbl_r_pengajuan_perusahaan_dokumen_tipe.AsNoTracking()
            .Where(d => d.is_aktif)
            .OrderBy(d => d.grup)
            .ThenBy(d => d.nama_dokumen)
            .ToListAsync(cancellationToken);

        var items = new List<PengajuanDokumenItem>();
        foreach (var doc in docs)
        {
            var req = reqs.FirstOrDefault(r => r.doc_type_id == doc.doc_type_id);
            items.Add(new PengajuanDokumenItem
            {
                ReqId = req?.req_id ?? 0,
                DocTypeId = doc.doc_type_id,
                Grup = doc.grup,
                NamaDokumen = doc.nama_dokumen,
                Deskripsi = doc.deskripsi,
                Wajib = req?.wajib ?? false,
                Status = req?.status ?? "optional",
                IsRequired = req?.wajib ?? false,
                SelectedRequired = req?.wajib ?? false
            });
        }

        var fileLookup = await _context.tbl_r_pengajuan_perusahaan_dokumen.AsNoTracking()
            .Where(d => reqs.Select(r => r.req_id).Contains(d.req_id))
            .ToDictionaryAsync(d => d.req_id, d => d, cancellationToken);

        foreach (var item in items)
        {
            if (item.ReqId == 0)
            {
                continue;
            }

            if (fileLookup.TryGetValue(item.ReqId, out var file))
            {
                item.FileUrl = file.path_file;
                item.Catatan = file.catatan;
                item.Status = file.status;
            }
        }

        return items;
    }

    private async Task<PengajuanReviewViewModel> BuildReviewViewModelAsync(tbl_r_pengajuan_perusahaan pengajuan, CancellationToken cancellationToken)
    {
        var docs = await BuildReviewDocItemsAsync(pengajuan.pengajuan_id, cancellationToken);
        var timeline = await BuildTimelineAsync(pengajuan.pengajuan_id, cancellationToken);
        var link = await _context.tbl_r_pengajuan_perusahaan_link.AsNoTracking()
            .Where(l => l.pengajuan_id == pengajuan.pengajuan_id && l.status == "active")
            .OrderByDescending(l => l.created_at)
            .FirstOrDefaultAsync(cancellationToken);

        var typeOptions = await _context.tbl_m_tipe_perusahaan.AsNoTracking()
            .OrderBy(t => t.nama_tipe)
            .Select(t => new SelectListItem
            {
                Text = t.nama_tipe ?? "-",
                Value = t.tipe_perusahaan_id.ToString()
            }).ToListAsync(cancellationToken);

        return new PengajuanReviewViewModel
        {
            PengajuanId = pengajuan.pengajuan_id,
            KodePengajuan = pengajuan.kode_pengajuan,
            NamaPerusahaan = pengajuan.nama_perusahaan,
            EmailPerusahaan = pengajuan.email_perusahaan,
            StatusPengajuan = pengajuan.status_pengajuan,
            ReviewerNote = pengajuan.reviewer_note,
            CatatanPengaju = pengajuan.catatan_pengaju,
            DokumenBelumLengkap = docs.Where(d => d.IsRequired).Any(d => d.Status != "uploaded"),
            Dokumen = docs,
            Timeline = timeline,
            PublicLinkUrl = link != null ? Url.Action(nameof(PublicLink), new { token = link.token }) : null,
            TipePerusahaanId = pengajuan.tipe_perusahaan_id,
            TipePerusahaanOptions = typeOptions
        };
    }

    private async Task<List<PengajuanTimelineItem>> BuildTimelineAsync(int pengajuanId, CancellationToken cancellationToken)
    {
        var logRows = await _context.tbl_r_pengajuan_perusahaan_log.AsNoTracking()
            .Where(l => l.pengajuan_id == pengajuanId)
            .Select(l => new { l.aktivitas, l.timestamp })
            .ToListAsync(cancellationToken);

        var logs = logRows.Select(l => new PengajuanTimelineItem
        {
            Judul = l.aktivitas,
            Catatan = null,
            Waktu = l.timestamp,
            StatusBadge = (l.aktivitas ?? string.Empty).IndexOf("disetujui", StringComparison.OrdinalIgnoreCase) >= 0
                ? "success"
                : (l.aktivitas ?? string.Empty).IndexOf("ditolak", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "danger"
                    : (l.aktivitas ?? string.Empty).IndexOf("perbaikan", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "warning"
                        : "secondary"
        }).ToList();

        var reviewRows = await _context.tbl_r_pengajuan_perusahaan_review.AsNoTracking()
            .Where(r => r.pengajuan_id == pengajuanId)
            .Select(r => new { r.status_review, r.catatan, r.action_date })
            .ToListAsync(cancellationToken);

        var reviews = reviewRows.Select(r => new PengajuanTimelineItem
        {
            Judul = r.status_review switch
            {
                "approved" => "Review: Disetujui",
                "ditolak" => "Review: Ditolak",
                "perlu_perbaikan" => "Review: Perlu Perbaikan",
                "menunggu_dokumen" => "Review: Rekomendasi",
                _ => $"Review {r.status_review}"
            },
            Catatan = r.catatan,
            Waktu = r.action_date,
            StatusBadge = r.status_review == "approved" ? "success"
                : r.status_review == "ditolak" ? "danger"
                : r.status_review == "perlu_perbaikan" ? "warning"
                : "secondary"
        }).ToList();

        return logs.Concat(reviews).OrderByDescending(t => t.Waktu).ToList();
    }

    private async Task EnsureCompanyAndUserAsync(tbl_r_pengajuan_perusahaan pengajuan, CancellationToken cancellationToken)
    {
        if (!pengajuan.perusahaan_id.HasValue || pengajuan.perusahaan_id.Value <= 0)
        {
            var company = new tbl_m_perusahaan
            {
                kode_perusahaan = null,
                nama_perusahaan = pengajuan.nama_perusahaan,
                alamat_perusahaan = pengajuan.alamat_lengkap,
                status_perusahaan = pengajuan.status_pengajuan == "approved_remark" ? "WARN" : "OK",
                tipe_perusahaan_id = pengajuan.tipe_perusahaan_id,
                perusahaan_induk_id = pengajuan.perusahaan_induk_id,
                is_aktif = true,
                created_by = User.Identity?.Name ?? "system",
                dibuat_pada = DateTime.UtcNow
            };
            _context.tbl_m_perusahaan.Add(company);
            await _context.SaveChangesAsync(cancellationToken);
            pengajuan.perusahaan_id = company.perusahaan_id;
        }

        var existingUser = await _context.tbl_m_pengguna.AsNoTracking()
            .FirstOrDefaultAsync(u => u.email == pengajuan.email_perusahaan, cancellationToken);
        if (existingUser != null)
        {
            return;
        }

        var username = pengajuan.email_perusahaan.Trim().ToLowerInvariant();
        var password = GeneratePassword();
        var roleId = pengajuan.tipe_perusahaan_id ?? 1;

        var user = new tbl_m_pengguna
        {
            username = username,
            kata_sandi = password,
            nama_lengkap = pengajuan.nama_perusahaan,
            email = pengajuan.email_perusahaan,
            perusahaan_id = pengajuan.perusahaan_id ?? 0,
            peran_id = roleId,
            is_aktif = true,
            dibuat_pada = DateTime.UtcNow
        };
        _context.tbl_m_pengguna.Add(user);

        _context.tbl_m_email_notifikasi.Add(new tbl_m_email_notifikasi
        {
            id = Guid.NewGuid().ToString("N"),
            email_to = pengajuan.email_perusahaan,
            subject = "Akun Perusahaan Disetujui",
            pesan_html = $"<p>Pengajuan perusahaan <strong>{System.Net.WebUtility.HtmlEncode(pengajuan.nama_perusahaan)}</strong> disetujui.</p>" +
                         $"<p>Username: <strong>{System.Net.WebUtility.HtmlEncode(username)}</strong><br/>Password: <strong>{System.Net.WebUtility.HtmlEncode(password)}</strong></p>" +
                         $"<p>Login: {System.Net.WebUtility.HtmlEncode($"{Request.Scheme}://{Request.Host}/Account/Login")}</p>",
            status = "queued",
            created_at = DateTime.UtcNow,
            created_by = User.Identity?.Name ?? "system"
        });
    }

    private async Task QueueReminderEmailsAsync(CancellationToken cancellationToken)
    {
        var pendingStatuses = new[] { "menunggu_dokumen", "perlu_perbaikan" };
        var pending = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
            .Where(p => pendingStatuses.Contains(p.status_pengajuan))
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        var reqStatus = await _context.tbl_r_pengajuan_perusahaan_dokumen_wajib.AsNoTracking()
            .Where(r => pending.Select(p => p.pengajuan_id).Contains(r.pengajuan_id))
            .ToListAsync(cancellationToken);

        var incompleteIds = reqStatus
            .Where(r => r.wajib && !string.Equals(r.status, "uploaded", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.pengajuan_id)
            .Distinct()
            .ToHashSet();

        if (incompleteIds.Count == 0)
        {
            return;
        }

        var recentLogs = await _context.tbl_r_pengajuan_perusahaan_log.AsNoTracking()
            .Where(l => incompleteIds.Contains(l.pengajuan_id)
                        && l.aktivitas == "Reminder dokumen")
            .GroupBy(l => l.pengajuan_id)
            .Select(g => new { PengajuanId = g.Key, LastReminder = g.Max(x => x.timestamp) })
            .ToListAsync(cancellationToken);

        var reminderMap = recentLogs.ToDictionary(r => r.PengajuanId, r => r.LastReminder);

        foreach (var pengajuan in pending.Where(p => incompleteIds.Contains(p.pengajuan_id)))
        {
            if (reminderMap.TryGetValue(pengajuan.pengajuan_id, out var last)
                && last > DateTime.UtcNow.AddHours(-24))
            {
                continue;
            }

            _context.tbl_m_email_notifikasi.Add(new tbl_m_email_notifikasi
            {
                id = Guid.NewGuid().ToString("N"),
                email_to = pengajuan.email_perusahaan,
                subject = "Pengingat Dokumen Pengajuan Perusahaan",
                pesan_html = $"<p>Dokumen pengajuan perusahaan <strong>{System.Net.WebUtility.HtmlEncode(pengajuan.nama_perusahaan)}</strong> masih belum lengkap.</p>" +
                             $"<p>Silakan lengkapi dokumen yang diminta Safety agar proses review berjalan.</p>",
                status = "queued",
                created_at = DateTime.UtcNow,
                created_by = "system"
            });

            _context.tbl_r_pengajuan_perusahaan_log.Add(new tbl_r_pengajuan_perusahaan_log
            {
                pengajuan_id = pengajuan.pengajuan_id,
                aktivitas = "Reminder dokumen",
                performed_by = "system",
                timestamp = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = new Random();
        return new string(Enumerable.Range(0, 8).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
    }

    private async Task<string> GenerateKodeAsync(CancellationToken cancellationToken)
    {
        var prefix = $"PGJ-{DateTime.UtcNow:yyMMdd}";
        var countToday = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
            .CountAsync(p => p.kode_pengajuan.StartsWith(prefix), cancellationToken);
        return $"{prefix}-{(countToday + 1).ToString().PadLeft(4, '0')}";
    }

    private static async Task<string?> SaveFileAsync(Microsoft.AspNetCore.Http.IFormFile file, string folder, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return null;
        }

        var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
        Directory.CreateDirectory(root);
        var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(root, safeName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);
        return $"/uploads/{folder}/{safeName}";
    }

    private async Task<RegistrationScope> BuildScopeAsync(CancellationToken cancellationToken)
    {
        var roleId = GetClaimInt("role_id");
        var companyId = GetClaimInt("company_id");
        var departmentId = GetOptionalClaimInt("department_id");

        var roleLevel = await _context.tbl_m_peran.AsNoTracking()
            .Where(r => r.peran_id == roleId)
            .Select(r => r.level_akses)
            .FirstOrDefaultAsync(cancellationToken);

        var hasDepartment = departmentId.HasValue && departmentId.Value > 0;
        string? departmentName = null;
        if (hasDepartment)
        {
            departmentName = await _context.tbl_m_departemen.AsNoTracking()
                .Where(d => d.departemen_id == departmentId!.Value)
                .Select(d => d.nama_departemen)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var isSafetyDept = !string.IsNullOrWhiteSpace(departmentName)
                           && departmentName.IndexOf("safety", StringComparison.OrdinalIgnoreCase) >= 0;
        var isOwner = roleLevel >= 4 && (!hasDepartment || isSafetyDept);
        var canSubmit = companyId > 0 && !hasDepartment;

        return new RegistrationScope
        {
            RoleLevel = roleLevel,
            CompanyId = companyId,
            IsOwner = isOwner,
            CanSubmit = canSubmit
        };
    }

    private async Task<(int ParentCompanyId, string ParentCompanyName, int? TargetTypeId, string TargetTypeName)> GetAutoCompanyTargetAsync(int companyId, CancellationToken cancellationToken)
    {
        if (companyId <= 0)
        {
            return (0, "-", null, "-");
        }

        var companyInfo = await (from company in _context.tbl_m_perusahaan.AsNoTracking()
                                 join type in _context.tbl_m_tipe_perusahaan.AsNoTracking()
                                     on company.tipe_perusahaan_id equals type.tipe_perusahaan_id into typeJoin
                                 from type in typeJoin.DefaultIfEmpty()
                                 where company.perusahaan_id == companyId
                                 select new
                                 {
                                     company.perusahaan_id,
                                     CompanyName = company.nama_perusahaan ?? "-",
                                     TypeName = type != null ? type.nama_tipe ?? "-" : "-"
                                 }).FirstOrDefaultAsync(cancellationToken);

        if (companyInfo == null)
        {
            return (companyId, "-", null, "-");
        }

        var targetTypeName = companyInfo.TypeName switch
        {
            var name when name.IndexOf("Owner", StringComparison.OrdinalIgnoreCase) >= 0 => "Main Contractor",
            var name when name.IndexOf("Main Contractor", StringComparison.OrdinalIgnoreCase) >= 0 => "Sub Contractor",
            var name when name.IndexOf("Sub Contractor", StringComparison.OrdinalIgnoreCase) >= 0 => "Vendor",
            _ => "-"
        };

        int? targetTypeId = null;
        if (!string.IsNullOrWhiteSpace(targetTypeName) && targetTypeName != "-")
        {
            targetTypeId = await _context.tbl_m_tipe_perusahaan.AsNoTracking()
                .Where(t => t.nama_tipe != null && t.nama_tipe.Contains(targetTypeName))
                .Select(t => (int?)t.tipe_perusahaan_id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return (companyInfo.perusahaan_id, companyInfo.CompanyName, targetTypeId, targetTypeName);
    }

    private static bool IsInitialDocType(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var lower = name.ToLowerInvariant().Replace("  ", " ").Trim();
        var allowed = new[]
        {
            "iujp (inti)",
            "skt / nib (non inti)",
            "kbli 2020",
            "npwp",
            "kontrak / spk / po",
            "struktur organisasi",
            "org card",
            "surat penunjukan pjo",
            "keanggotaan bpjs",
            "dokumen lainnya"
        };

        return allowed.Any(item => lower.Contains(item));
    }

    private int GetClaimInt(string key)
    {
        var value = User.FindFirstValue(key);
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private int? GetOptionalClaimInt(string key)
    {
        var value = User.FindFirstValue(key);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed class RegistrationScope
    {
        public int RoleLevel { get; set; }
        public int CompanyId { get; set; }
        public bool IsOwner { get; set; }
        public bool CanSubmit { get; set; }
    }
}
