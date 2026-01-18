using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;
using one_db_mitra.Services.CompanyHierarchy;

namespace one_db_mitra.Controllers
{
    [Authorize]
    public class OrganizationAdminController : Controller
    {
        private readonly OneDbMitraContext _context;
        private readonly Services.Audit.AuditLogger _auditLogger;
        private readonly CompanyHierarchyService _companyHierarchyService;

        public OrganizationAdminController(OneDbMitraContext context, Services.Audit.AuditLogger auditLogger, CompanyHierarchyService companyHierarchyService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _companyHierarchyService = companyHierarchyService;
        }

        [HttpGet]
        public async Task<IActionResult> Companies(bool activeOnly = true, string? search = null, CancellationToken cancellationToken = default)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!scope.IsOwner && scope.CompanyId <= 0)
            {
                SetAlert("Tidak memiliki akses ke data perusahaan.", "warning");
                return View(Array.Empty<CompanyListItem>());
            }

            ViewBag.ScopeInfo = await BuildScopeInfoAsync(scope, cancellationToken);
            var baseQuery = _context.vw_hirarki_perusahaan_arrow.AsNoTracking().AsQueryable();
            if (scope.AllowedCompanyIds.Count > 0)
            {
                baseQuery = baseQuery.Where(company => scope.AllowedCompanyIds.Contains(company.id_perusahaan));
            }
            else if (!scope.IsOwner)
            {
                if (scope.CompanyId > 0)
                {
                    baseQuery = baseQuery.Where(company => company.id_perusahaan == scope.CompanyId);
                }
                else
                {
                    baseQuery = baseQuery.Where(company => false);
                }
            }

            if (activeOnly)
            {
                baseQuery = from view in baseQuery
                            join company in _context.tbl_m_perusahaan.AsNoTracking()
                                on view.id_perusahaan equals company.perusahaan_id
                            where company.is_aktif
                            select view;
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                baseQuery = baseQuery.Where(company =>
                    (company.nama_perusahaan != null && company.nama_perusahaan.Contains(term))
                    || (company.kode_perusahaan != null && company.kode_perusahaan.Contains(term)));
            }

            var companies = await (from view in baseQuery
                                   join company in _context.tbl_m_perusahaan.AsNoTracking()
                                       on view.id_perusahaan equals company.perusahaan_id
                                   join parent in _context.tbl_m_perusahaan.AsNoTracking()
                                       on company.perusahaan_induk_id equals parent.perusahaan_id into parentJoin
                                   from parent in parentJoin.DefaultIfEmpty()
                                   orderby view.id_perusahaan descending
                                   select new CompanyListItem
                                   {
                                       CompanyId = view.id_perusahaan,
                                       CompanyCode = view.kode_perusahaan ?? string.Empty,
                                       CompanyName = view.nama_perusahaan ?? string.Empty,
                                       CompanyTypeId = view.id_jenis_perusahaan,
                                       CompanyTypeName = view.nama_tipe ?? "-",
                                       ParentCompanyName = parent != null ? parent.nama_perusahaan ?? "-" : "-",
                                       IsActive = company.is_aktif
                                   }).ToListAsync(cancellationToken);

            var approvedCompanies = await _context.tbl_r_pengajuan_perusahaan.AsNoTracking()
                .Where(p => p.status_pengajuan == "approved" || p.status_pengajuan == "approved_remark")
                .Select(p => p.perusahaan_id ?? 0)
                .ToListAsync(cancellationToken);
            var approvedSet = new HashSet<int>(approvedCompanies);

            foreach (var item in companies)
            {
                if (!approvedSet.Contains(item.CompanyId))
                {
                    item.DokumenBelumLengkap = true;
                    item.RemarkDokumen = "Data belum lengkap (legacy)";
                }
            }

            ViewBag.ActiveOnly = activeOnly;
            ViewBag.CurrentSearch = search;
            return View(companies);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCompany(CancellationToken cancellationToken)
        {
            SetAlert("Penambahan perusahaan dilakukan melalui menu Pengajuan Perusahaan.", "warning");
            return RedirectToAction(nameof(Companies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCompany(CompanyEditViewModel model, CancellationToken cancellationToken)
        {
            SetAlert("Penambahan perusahaan dilakukan melalui menu Pengajuan Perusahaan.", "warning");
            return RedirectToAction(nameof(Companies));
        }

        [HttpGet]
        public async Task<IActionResult> EditCompany(int id, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!scope.IsOwner)
            {
                SetAlert("Hanya Owner yang dapat mengubah perusahaan.", "warning");
                return RedirectToAction(nameof(Companies));
            }

            var company = await _context.tbl_m_perusahaan.AsNoTracking()
                .FirstOrDefaultAsync(c => c.perusahaan_id == id, cancellationToken);
            if (company is null)
            {
                return NotFound();
            }

            var model = new CompanyEditViewModel
            {
                CompanyId = company.perusahaan_id,
                CompanyCode = company.kode_perusahaan,
                CompanyName = company.nama_perusahaan ?? string.Empty,
                CompanyAddress = company.alamat_perusahaan,
                CompanyStatus = company.status_perusahaan,
                CompanyTypeId = company.tipe_perusahaan_id,
                ParentCompanyId = company.perusahaan_induk_id,
                IsActive = company.is_aktif
            };

            await PopulateCompanyOptionsAsync(model, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCompany(CompanyEditViewModel model, CancellationToken cancellationToken)
        {
            SetAlert("Perubahan perusahaan dinonaktifkan. Gunakan Pengajuan Perusahaan jika ada pembaruan.", "warning");
            return RedirectToAction(nameof(Companies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCompany(int id, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            SetAlert("Penghapusan perusahaan dinonaktifkan.", "warning");
            return RedirectToAction(nameof(Companies), new { activeOnly });
        }

        [HttpGet]
        public async Task<IActionResult> Departments(bool activeOnly = true, string? search = null, int? companyId = null, CancellationToken cancellationToken = default)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            ViewBag.ScopeInfo = await BuildScopeInfoAsync(scope, cancellationToken);
            var baseQuery = ApplyDepartmentScope(_context.tbl_m_departemen.AsNoTracking().AsQueryable(), scope);
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(dept => dept.is_aktif == true);
            }
            if (companyId.HasValue && companyId.Value > 0)
            {
                baseQuery = baseQuery.Where(dept => dept.perusahaan_id == companyId.Value);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                baseQuery = baseQuery.Where(dept => dept.nama_departemen != null && dept.nama_departemen.Contains(term));
            }

            var departments = await (from dept in baseQuery
                                     join company in _context.tbl_m_perusahaan.AsNoTracking() on dept.perusahaan_id equals company.perusahaan_id
                                     orderby dept.departemen_id descending
                                     select new DepartmentListItem
                                     {
                                         DepartmentId = dept.departemen_id,
                                         DepartmentCode = dept.kode_departemen ?? string.Empty,
                                         DepartmentName = dept.nama_departemen ?? string.Empty,
                                         CompanyName = company.nama_perusahaan ?? string.Empty,
                                         IsActive = dept.is_aktif == true
                                     }).ToListAsync(cancellationToken);

            ViewBag.ActiveOnly = activeOnly;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCompanyId = companyId;
            var companyQuery = ApplyCompanyScope(_context.tbl_m_perusahaan.AsNoTracking().AsQueryable(), scope);
            var companyOptions = await companyQuery
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);
            companyOptions.Insert(0, new SelectListItem("Semua perusahaan", ""));
            var selectedCompany = companyId?.ToString() ?? string.Empty;
            foreach (var option in companyOptions)
            {
                option.Selected = option.Value == selectedCompany;
            }
            ViewBag.CompanyOptions = companyOptions;
            return View(departments);
        }

        [HttpGet]
        public async Task<IActionResult> CreateDepartment(CancellationToken cancellationToken)
        {
            var model = new DepartmentEditViewModel { IsActive = true };
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (scope.IsDepartmentAdmin)
            {
                SetAlert("Admin departemen tidak dapat menambah departemen baru.", "warning");
                return RedirectToAction(nameof(Departments));
            }

            await PopulateDepartmentOptionsAsync(model, scope, cancellationToken);
            ApplyDepartmentScopeToModel(model, scope);
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                ViewBag.LockCompanyId = scope.CompanyId;
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDepartment(DepartmentEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (scope.IsDepartmentAdmin)
            {
                SetAlert("Admin departemen tidak dapat menambah departemen baru.", "warning");
                return RedirectToAction(nameof(Departments));
            }

            if (!ModelState.IsValid)
            {
                await PopulateDepartmentOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                return View(model);
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && model.CompanyId != scope.CompanyId)
            {
                ModelState.AddModelError(nameof(model.CompanyId), "Perusahaan tidak sesuai akses.");
                await PopulateDepartmentOptionsAsync(model, scope, cancellationToken);
                return View(model);
            }

            var exists = await _context.tbl_m_departemen.AsNoTracking()
                .AnyAsync(d => d.perusahaan_id == model.CompanyId && d.nama_departemen == model.DepartmentName, cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.DepartmentName), "Nama departemen sudah digunakan.");
                await PopulateDepartmentOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                return View(model);
            }

            var entity = new Models.Db.tbl_m_departemen
            {
                kode_departemen = string.IsNullOrWhiteSpace(model.DepartmentCode) ? null : model.DepartmentCode.Trim(),
                nama_departemen = model.DepartmentName.Trim(),
                keterangan = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                perusahaan_id = model.CompanyId,
                is_aktif = model.IsActive,
                dibuat_pada = DateTime.UtcNow
            };

            _context.tbl_m_departemen.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("CREATE", "departemen", entity.departemen_id.ToString(), $"Tambah departemen {entity.nama_departemen}", cancellationToken);
            SetAlert("Departemen berhasil ditambahkan.");
            return RedirectToAction(nameof(Departments));
        }

        [HttpGet]
        public async Task<IActionResult> EditDepartment(int id, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            var dept = await _context.tbl_m_departemen.AsNoTracking()
                .FirstOrDefaultAsync(d => d.departemen_id == id, cancellationToken);
            if (dept is null)
            {
                return NotFound();
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && dept.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke departemen ini.", "warning");
                return RedirectToAction(nameof(Departments));
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue && dept.departemen_id != scope.DepartmentId.Value)
            {
                SetAlert("Tidak memiliki akses ke departemen ini.", "warning");
                return RedirectToAction(nameof(Departments));
            }

            var model = new DepartmentEditViewModel
            {
                DepartmentId = dept.departemen_id,
                DepartmentCode = dept.kode_departemen,
                DepartmentName = dept.nama_departemen ?? string.Empty,
                Description = dept.keterangan,
                CompanyId = dept.perusahaan_id ?? 0,
                IsActive = dept.is_aktif == true
            };

            await PopulateDepartmentOptionsAsync(model, scope, cancellationToken);
            ApplyDepartmentScopeToModel(model, scope);
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                ViewBag.LockCompanyId = scope.CompanyId;
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDepartment(DepartmentEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!ModelState.IsValid)
            {
                await PopulateDepartmentOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                return View(model);
            }

            var dept = await _context.tbl_m_departemen.FirstOrDefaultAsync(d => d.departemen_id == model.DepartmentId, cancellationToken);
            if (dept is null)
            {
                SetAlert("Departemen tidak ditemukan.", "warning");
                return NotFound();
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && dept.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke departemen ini.", "warning");
                return RedirectToAction(nameof(Departments));
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue && dept.departemen_id != scope.DepartmentId.Value)
            {
                SetAlert("Tidak memiliki akses ke departemen ini.", "warning");
                return RedirectToAction(nameof(Departments));
            }

            var exists = await _context.tbl_m_departemen.AsNoTracking()
                .AnyAsync(d => d.perusahaan_id == model.CompanyId && d.nama_departemen == model.DepartmentName && d.departemen_id != model.DepartmentId, cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.DepartmentName), "Nama departemen sudah digunakan.");
                await PopulateDepartmentOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                return View(model);
            }

            dept.kode_departemen = string.IsNullOrWhiteSpace(model.DepartmentCode) ? null : model.DepartmentCode.Trim();
            dept.nama_departemen = model.DepartmentName.Trim();
            dept.keterangan = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            dept.perusahaan_id = model.CompanyId;
            dept.is_aktif = model.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("UPDATE", "departemen", dept.departemen_id.ToString(), $"Ubah departemen {dept.nama_departemen}", cancellationToken);
            SetAlert("Departemen berhasil diperbarui.");
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartment(int id, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            var dept = await _context.tbl_m_departemen.FirstOrDefaultAsync(d => d.departemen_id == id, cancellationToken);
            if (dept is null)
            {
                SetAlert("Departemen tidak ditemukan.", "warning");
                return RedirectToAction(nameof(Departments), new { activeOnly });
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && dept.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke departemen ini.", "warning");
                return RedirectToAction(nameof(Departments), new { activeOnly });
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue && dept.departemen_id != scope.DepartmentId.Value)
            {
                SetAlert("Tidak memiliki akses ke departemen ini.", "warning");
                return RedirectToAction(nameof(Departments), new { activeOnly });
            }

            var hasSections = await _context.tbl_m_seksi.AsNoTracking()
                .AnyAsync(s => s.departemen_id == id && s.is_aktif == true, cancellationToken);
            var hasUsers = await _context.tbl_m_pengguna.AsNoTracking()
                .AnyAsync(u => u.departemen_id == id && u.is_aktif, cancellationToken);
            if (hasSections || hasUsers)
            {
                var sectionNames = await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.departemen_id == id && s.is_aktif == true)
                    .OrderBy(s => s.nama_seksi)
                    .Select(s => s.nama_seksi)
                    .Take(3)
                    .ToListAsync(cancellationToken);
                var userNames = await _context.tbl_m_pengguna.AsNoTracking()
                    .Where(u => u.departemen_id == id && u.is_aktif)
                    .OrderBy(u => u.nama_lengkap ?? u.username)
                    .Select(u => u.nama_lengkap ?? u.username ?? "User")
                    .Take(3)
                    .ToListAsync(cancellationToken);
                var detailParts = new System.Collections.Generic.List<string>();
                if (sectionNames.Count > 0)
                {
                    detailParts.Add($"Section: {string.Join(", ", sectionNames)}");
                }
                if (userNames.Count > 0)
                {
                    detailParts.Add($"User: {string.Join(", ", userNames)}");
                }
                var detail = detailParts.Count > 0 ? $" Contoh: {string.Join(" | ", detailParts)}." : string.Empty;
                SetAlert($"Departemen tidak bisa dihapus karena masih digunakan.{detail}", "warning");
                return RedirectToAction(nameof(Departments), new { activeOnly });
            }

            dept.is_aktif = false;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("DELETE", "departemen", dept.departemen_id.ToString(), $"Hapus departemen {dept.nama_departemen}", cancellationToken);
            SetAlert("Departemen berhasil dinonaktifkan.");
            return RedirectToAction(nameof(Departments), new { activeOnly });
        }

        [HttpGet]
        public async Task<IActionResult> Sections(bool activeOnly = true, string? search = null, int? companyId = null, CancellationToken cancellationToken = default)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            ViewBag.ScopeInfo = await BuildScopeInfoAsync(scope, cancellationToken);
            var baseQuery = ApplySectionScope(_context.tbl_m_seksi.AsNoTracking().AsQueryable(), scope);
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(section => section.is_aktif == true);
            }
            if (companyId.HasValue && companyId.Value > 0)
            {
                baseQuery = baseQuery.Where(section => section.perusahaan_id == companyId.Value);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                baseQuery = baseQuery.Where(section => section.nama_seksi != null && section.nama_seksi.Contains(term));
            }

            var sections = await (from section in baseQuery
                                  join dept in _context.tbl_m_departemen.AsNoTracking() on section.departemen_id equals dept.departemen_id
                                  join company in _context.tbl_m_perusahaan.AsNoTracking() on section.perusahaan_id equals company.perusahaan_id
                                  orderby section.seksi_id descending
                                  select new SectionListItem
                                  {
                                      SectionId = section.seksi_id,
                                      SectionCode = section.kode_seksi ?? string.Empty,
                                      SectionName = section.nama_seksi ?? string.Empty,
                                      DepartmentName = dept.nama_departemen ?? string.Empty,
                                      CompanyName = company.nama_perusahaan ?? string.Empty,
                                      IsActive = section.is_aktif == true
                                  }).ToListAsync(cancellationToken);

            ViewBag.ActiveOnly = activeOnly;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCompanyId = companyId;
            var companyQuery = ApplyCompanyScope(_context.tbl_m_perusahaan.AsNoTracking().AsQueryable(), scope);
            var companyOptions = await companyQuery
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);
            companyOptions.Insert(0, new SelectListItem("Semua perusahaan", ""));
            var selectedCompany = companyId?.ToString() ?? string.Empty;
            foreach (var option in companyOptions)
            {
                option.Selected = option.Value == selectedCompany;
            }
            ViewBag.CompanyOptions = companyOptions;
            return View(sections);
        }

        [HttpGet]
        public async Task<IActionResult> CreateSection(CancellationToken cancellationToken)
        {
            var model = new SectionEditViewModel { IsActive = true };
            var scope = await BuildOrgScopeAsync(cancellationToken);
            await PopulateSectionOptionsAsync(model, scope, cancellationToken);
            ApplySectionScopeToModel(model, scope);
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                ViewBag.LockCompanyId = scope.CompanyId;
            }
            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                ViewBag.LockDepartmentId = scope.DepartmentId.Value;
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection(SectionEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!ModelState.IsValid)
            {
                await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                {
                    ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                }
                return View(model);
            }

            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                var deptCompany = await _context.tbl_m_departemen.AsNoTracking()
                    .Where(d => d.departemen_id == model.DepartmentId)
                    .Select(d => d.perusahaan_id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (deptCompany != scope.CompanyId)
                {
                    ModelState.AddModelError(nameof(model.DepartmentId), "Departemen tidak sesuai akses.");
                    await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                    if (!scope.IsOwner && scope.CompanyId > 0)
                    {
                        ViewBag.LockCompanyId = scope.CompanyId;
                    }
                    if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                    {
                        ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                    }
                    return View(model);
                }
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue && model.DepartmentId != scope.DepartmentId.Value)
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Departemen tidak sesuai akses.");
                await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                {
                    ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                }
                return View(model);
            }

            var exists = await _context.tbl_m_seksi.AsNoTracking()
                .AnyAsync(s => s.departemen_id == model.DepartmentId && s.nama_seksi == model.SectionName, cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.SectionName), "Nama section sudah digunakan.");
                await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                {
                    ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                }
                return View(model);
            }

            var department = await _context.tbl_m_departemen.AsNoTracking()
                .FirstOrDefaultAsync(d => d.departemen_id == model.DepartmentId, cancellationToken);
            if (department is null)
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Departemen tidak valid.");
                await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                {
                    ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                }
                return View(model);
            }

            var entity = new Models.Db.tbl_m_seksi
            {
                kode_seksi = string.IsNullOrWhiteSpace(model.SectionCode) ? null : model.SectionCode.Trim(),
                nama_seksi = model.SectionName.Trim(),
                keterangan = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                departemen_id = model.DepartmentId,
                perusahaan_id = department.perusahaan_id,
                is_aktif = model.IsActive,
                dibuat_pada = DateTime.UtcNow
            };

            _context.tbl_m_seksi.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("CREATE", "seksi", entity.seksi_id.ToString(), $"Tambah section {entity.nama_seksi}", cancellationToken);
            SetAlert("Section berhasil ditambahkan.");
            return RedirectToAction(nameof(Sections));
        }

        [HttpGet]
        public async Task<IActionResult> EditSection(int id, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            var section = await _context.tbl_m_seksi.AsNoTracking()
                .FirstOrDefaultAsync(s => s.seksi_id == id, cancellationToken);
            if (section is null)
            {
                return NotFound();
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && section.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke section ini.", "warning");
                return RedirectToAction(nameof(Sections));
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue && section.departemen_id != scope.DepartmentId.Value)
            {
                SetAlert("Tidak memiliki akses ke section ini.", "warning");
                return RedirectToAction(nameof(Sections));
            }

            var model = new SectionEditViewModel
            {
                SectionId = section.seksi_id,
                SectionCode = section.kode_seksi,
                SectionName = section.nama_seksi ?? string.Empty,
                Description = section.keterangan,
                DepartmentId = section.departemen_id ?? 0,
                CompanyId = section.perusahaan_id ?? 0,
                IsActive = section.is_aktif == true
            };

            await PopulateSectionOptionsAsync(model, scope, cancellationToken);
            ApplySectionScopeToModel(model, scope);
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                ViewBag.LockCompanyId = scope.CompanyId;
            }
            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                ViewBag.LockDepartmentId = scope.DepartmentId.Value;
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSection(SectionEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!ModelState.IsValid)
            {
                await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                {
                    ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                }
                return View(model);
            }

            var section = await _context.tbl_m_seksi.FirstOrDefaultAsync(s => s.seksi_id == model.SectionId, cancellationToken);
            if (section is null)
            {
                SetAlert("Section tidak ditemukan.", "warning");
                return NotFound();
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && section.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke section ini.", "warning");
                return RedirectToAction(nameof(Sections));
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue && section.departemen_id != scope.DepartmentId.Value)
            {
                SetAlert("Tidak memiliki akses ke section ini.", "warning");
                return RedirectToAction(nameof(Sections));
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                model.DepartmentId = section.departemen_id ?? model.DepartmentId;
            }

            var exists = await _context.tbl_m_seksi.AsNoTracking()
                .AnyAsync(s => s.departemen_id == model.DepartmentId && s.nama_seksi == model.SectionName && s.seksi_id != model.SectionId, cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.SectionName), "Nama section sudah digunakan.");
                await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                {
                    ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                }
                return View(model);
            }

            var department = await _context.tbl_m_departemen.AsNoTracking()
                .FirstOrDefaultAsync(d => d.departemen_id == model.DepartmentId, cancellationToken);
            if (department is null)
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Departemen tidak valid.");
                await PopulateSectionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
                {
                    ViewBag.LockDepartmentId = scope.DepartmentId.Value;
                }
                return View(model);
            }

            section.kode_seksi = string.IsNullOrWhiteSpace(model.SectionCode) ? null : model.SectionCode.Trim();
            section.nama_seksi = model.SectionName.Trim();
            section.keterangan = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            section.departemen_id = model.DepartmentId;
            section.perusahaan_id = department.perusahaan_id;
            section.is_aktif = model.IsActive;
            section.diubah_pada = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("UPDATE", "seksi", section.seksi_id.ToString(), $"Ubah section {section.nama_seksi}", cancellationToken);
            SetAlert("Section berhasil diperbarui.");
            return RedirectToAction(nameof(Sections));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSection(int id, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            var section = await _context.tbl_m_seksi.FirstOrDefaultAsync(s => s.seksi_id == id, cancellationToken);
            if (section is null)
            {
                SetAlert("Section tidak ditemukan.", "warning");
                return RedirectToAction(nameof(Sections), new { activeOnly });
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && section.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke section ini.", "warning");
                return RedirectToAction(nameof(Sections), new { activeOnly });
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue && section.departemen_id != scope.DepartmentId.Value)
            {
                SetAlert("Tidak memiliki akses ke section ini.", "warning");
                return RedirectToAction(nameof(Sections), new { activeOnly });
            }

            var hasPositions = await _context.tbl_m_jabatan.AsNoTracking()
                .AnyAsync(p => p.seksi_id == id && p.is_aktif == true, cancellationToken);
            var hasUsers = await _context.tbl_m_pengguna.AsNoTracking()
                .AnyAsync(u => u.seksi_id == id && u.is_aktif, cancellationToken);
            if (hasPositions || hasUsers)
            {
                var positionNames = await _context.tbl_m_jabatan.AsNoTracking()
                    .Where(p => p.seksi_id == id && p.is_aktif == true)
                    .OrderBy(p => p.nama_jabatan)
                    .Select(p => p.nama_jabatan)
                    .Take(3)
                    .ToListAsync(cancellationToken);
                var userNames = await _context.tbl_m_pengguna.AsNoTracking()
                    .Where(u => u.seksi_id == id && u.is_aktif)
                    .OrderBy(u => u.nama_lengkap ?? u.username)
                    .Select(u => u.nama_lengkap ?? u.username ?? "User")
                    .Take(3)
                    .ToListAsync(cancellationToken);
                var detailParts = new System.Collections.Generic.List<string>();
                if (positionNames.Count > 0)
                {
                    detailParts.Add($"Jabatan: {string.Join(", ", positionNames)}");
                }
                if (userNames.Count > 0)
                {
                    detailParts.Add($"User: {string.Join(", ", userNames)}");
                }
                var detail = detailParts.Count > 0 ? $" Contoh: {string.Join(" | ", detailParts)}." : string.Empty;
                SetAlert($"Section tidak bisa dihapus karena masih digunakan.{detail}", "warning");
                return RedirectToAction(nameof(Sections), new { activeOnly });
            }

            section.is_aktif = false;
            section.diubah_pada = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("DELETE", "seksi", section.seksi_id.ToString(), $"Hapus section {section.nama_seksi}", cancellationToken);
            SetAlert("Section berhasil dinonaktifkan.");
            return RedirectToAction(nameof(Sections), new { activeOnly });
        }

        [HttpGet]
        public async Task<IActionResult> Positions(bool activeOnly = true, string? search = null, int? companyId = null, CancellationToken cancellationToken = default)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            ViewBag.ScopeInfo = await BuildScopeInfoAsync(scope, cancellationToken);
            var baseQuery = ApplyPositionScope(_context.tbl_m_jabatan.AsNoTracking().AsQueryable(), scope);
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(position => position.is_aktif == true);
            }
            if (companyId.HasValue && companyId.Value > 0)
            {
                baseQuery = baseQuery.Where(position => position.perusahaan_id == companyId.Value);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                baseQuery = baseQuery.Where(position => position.nama_jabatan != null && position.nama_jabatan.Contains(term));
            }

            var positions = await (from position in baseQuery
                                   join section in _context.tbl_m_seksi.AsNoTracking() on position.seksi_id equals section.seksi_id into sectionJoin
                                   from section in sectionJoin.DefaultIfEmpty()
                                   join company in _context.tbl_m_perusahaan.AsNoTracking() on position.perusahaan_id equals company.perusahaan_id
                                   orderby position.jabatan_id descending
                                   select new PositionListItem
                                   {
                                       PositionId = position.jabatan_id,
                                       PositionCode = position.kode_jabatan ?? string.Empty,
                                       PositionName = position.nama_jabatan ?? string.Empty,
                                       SectionName = section != null ? section.nama_seksi ?? "-" : "-",
                                       CompanyName = company.nama_perusahaan ?? string.Empty,
                                       IsActive = position.is_aktif == true
                                   }).ToListAsync(cancellationToken);

            ViewBag.ActiveOnly = activeOnly;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCompanyId = companyId;
            var companyQuery = ApplyCompanyScope(_context.tbl_m_perusahaan.AsNoTracking().AsQueryable(), scope);
            var companyOptions = await companyQuery
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);
            companyOptions.Insert(0, new SelectListItem("Semua perusahaan", ""));
            var selectedCompany = companyId?.ToString() ?? string.Empty;
            foreach (var option in companyOptions)
            {
                option.Selected = option.Value == selectedCompany;
            }
            ViewBag.CompanyOptions = companyOptions;
            return View(positions);
        }

        [HttpGet]
        public async Task<IActionResult> CreatePosition(CancellationToken cancellationToken)
        {
            var model = new PositionEditViewModel { IsActive = true };
            var scope = await BuildOrgScopeAsync(cancellationToken);
            await PopulatePositionOptionsAsync(model, scope, cancellationToken);
            ApplyPositionScopeToModel(model, scope);
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                ViewBag.LockCompanyId = scope.CompanyId;
            }
            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                ViewBag.LockSectionId = scope.SectionId.Value;
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePosition(PositionEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!ModelState.IsValid)
            {
                await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
                {
                    ViewBag.LockSectionId = scope.SectionId.Value;
                }
                return View(model);
            }

            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                var sectionCompany = await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.seksi_id == model.SectionId)
                    .Select(s => s.perusahaan_id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (sectionCompany != scope.CompanyId)
                {
                    ModelState.AddModelError(nameof(model.SectionId), "Section tidak sesuai akses.");
                    await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                    return View(model);
                }
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                var sectionDept = await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.seksi_id == model.SectionId)
                    .Select(s => s.departemen_id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (sectionDept != scope.DepartmentId.Value)
                {
                    ModelState.AddModelError(nameof(model.SectionId), "Section tidak sesuai akses.");
                    await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                    return View(model);
                }
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0 && model.SectionId != scope.SectionId.Value)
            {
                ModelState.AddModelError(nameof(model.SectionId), "Section tidak sesuai akses.");
                await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                return View(model);
            }

            var exists = await _context.tbl_m_jabatan.AsNoTracking()
                .AnyAsync(p => p.seksi_id == model.SectionId && p.nama_jabatan == model.PositionName, cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.PositionName), "Nama jabatan sudah digunakan.");
                await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
                {
                    ViewBag.LockSectionId = scope.SectionId.Value;
                }
                return View(model);
            }

            var section = await _context.tbl_m_seksi.AsNoTracking()
                .FirstOrDefaultAsync(s => s.seksi_id == model.SectionId, cancellationToken);
            if (section is null)
            {
                ModelState.AddModelError(nameof(model.SectionId), "Section tidak valid.");
                await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
                {
                    ViewBag.LockSectionId = scope.SectionId.Value;
                }
                return View(model);
            }

            var entity = new Models.Db.tbl_m_jabatan
            {
                kode_jabatan = string.IsNullOrWhiteSpace(model.PositionCode) ? null : model.PositionCode.Trim(),
                nama_jabatan = model.PositionName.Trim(),
                keterangan = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                seksi_id = model.SectionId,
                perusahaan_id = section.perusahaan_id,
                is_aktif = model.IsActive,
                dibuat_pada = DateTime.UtcNow
            };

            _context.tbl_m_jabatan.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("CREATE", "jabatan", entity.jabatan_id.ToString(), $"Tambah jabatan {entity.nama_jabatan}", cancellationToken);
            SetAlert("Jabatan berhasil ditambahkan.");
            return RedirectToAction(nameof(Positions));
        }

        [HttpGet]
        public async Task<IActionResult> EditPosition(int id, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            var position = await _context.tbl_m_jabatan.AsNoTracking()
                .FirstOrDefaultAsync(p => p.jabatan_id == id, cancellationToken);
            if (position is null)
            {
                return NotFound();
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && position.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                return RedirectToAction(nameof(Positions));
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0 && position.seksi_id != scope.SectionId.Value)
            {
                SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                return RedirectToAction(nameof(Positions));
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                var sectionDept = await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.seksi_id == position.seksi_id)
                    .Select(s => s.departemen_id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (sectionDept != scope.DepartmentId.Value)
                {
                    SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                    return RedirectToAction(nameof(Positions));
                }
            }

            var model = new PositionEditViewModel
            {
                PositionId = position.jabatan_id,
                PositionCode = position.kode_jabatan,
                PositionName = position.nama_jabatan ?? string.Empty,
                Description = position.keterangan,
                SectionId = position.seksi_id,
                CompanyId = position.perusahaan_id ?? 0,
                IsActive = position.is_aktif == true
            };

            await PopulatePositionOptionsAsync(model, scope, cancellationToken);
            ApplyPositionScopeToModel(model, scope);
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                ViewBag.LockCompanyId = scope.CompanyId;
            }
            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                ViewBag.LockSectionId = scope.SectionId.Value;
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPosition(PositionEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!ModelState.IsValid)
            {
                await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
                {
                    ViewBag.LockSectionId = scope.SectionId.Value;
                }
                return View(model);
            }

            var position = await _context.tbl_m_jabatan.FirstOrDefaultAsync(p => p.jabatan_id == model.PositionId, cancellationToken);
            if (position is null)
            {
                SetAlert("Jabatan tidak ditemukan.", "warning");
                return NotFound();
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && position.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                return RedirectToAction(nameof(Positions));
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0 && position.seksi_id != scope.SectionId.Value)
            {
                SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                return RedirectToAction(nameof(Positions));
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                var sectionDept = await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.seksi_id == position.seksi_id)
                    .Select(s => s.departemen_id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (sectionDept != scope.DepartmentId.Value)
                {
                    SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                    return RedirectToAction(nameof(Positions));
                }
            }

            var exists = await _context.tbl_m_jabatan.AsNoTracking()
                .AnyAsync(p => p.seksi_id == model.SectionId && p.nama_jabatan == model.PositionName && p.jabatan_id != model.PositionId, cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.PositionName), "Nama jabatan sudah digunakan.");
                await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
                {
                    ViewBag.LockSectionId = scope.SectionId.Value;
                }
                return View(model);
            }

            var section = await _context.tbl_m_seksi.AsNoTracking()
                .FirstOrDefaultAsync(s => s.seksi_id == model.SectionId, cancellationToken);
            if (section is null)
            {
                ModelState.AddModelError(nameof(model.SectionId), "Section tidak valid.");
                await PopulatePositionOptionsAsync(model, scope, cancellationToken);
                if (!scope.IsOwner && scope.CompanyId > 0)
                {
                    ViewBag.LockCompanyId = scope.CompanyId;
                }
                if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
                {
                    ViewBag.LockSectionId = scope.SectionId.Value;
                }
                return View(model);
            }

            position.kode_jabatan = string.IsNullOrWhiteSpace(model.PositionCode) ? null : model.PositionCode.Trim();
            position.nama_jabatan = model.PositionName.Trim();
            position.keterangan = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            position.seksi_id = model.SectionId;
            position.perusahaan_id = section.perusahaan_id;
            position.is_aktif = model.IsActive;
            position.diubah_pada = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("UPDATE", "jabatan", position.jabatan_id.ToString(), $"Ubah jabatan {position.nama_jabatan}", cancellationToken);
            SetAlert("Jabatan berhasil diperbarui.");
            return RedirectToAction(nameof(Positions));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePosition(int id, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            var position = await _context.tbl_m_jabatan.FirstOrDefaultAsync(p => p.jabatan_id == id, cancellationToken);
            if (position is null)
            {
                SetAlert("Jabatan tidak ditemukan.", "warning");
                return RedirectToAction(nameof(Positions), new { activeOnly });
            }

            if (!scope.IsOwner && scope.CompanyId > 0 && position.perusahaan_id != scope.CompanyId)
            {
                SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                return RedirectToAction(nameof(Positions), new { activeOnly });
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0 && position.seksi_id != scope.SectionId.Value)
            {
                SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                return RedirectToAction(nameof(Positions), new { activeOnly });
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                var sectionDept = await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.seksi_id == position.seksi_id)
                    .Select(s => s.departemen_id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (sectionDept != scope.DepartmentId.Value)
                {
                    SetAlert("Tidak memiliki akses ke jabatan ini.", "warning");
                    return RedirectToAction(nameof(Positions), new { activeOnly });
                }
            }

            var hasUsers = await _context.tbl_m_pengguna.AsNoTracking()
                .AnyAsync(u => u.jabatan_id == id && u.is_aktif, cancellationToken);
            if (hasUsers)
            {
                var userNames = await _context.tbl_m_pengguna.AsNoTracking()
                    .Where(u => u.jabatan_id == id && u.is_aktif)
                    .OrderBy(u => u.nama_lengkap ?? u.username)
                    .Select(u => u.nama_lengkap ?? u.username ?? "User")
                    .Take(3)
                    .ToListAsync(cancellationToken);
                var detail = userNames.Count > 0 ? $" Contoh: User {string.Join(", ", userNames)}." : string.Empty;
                SetAlert($"Jabatan tidak bisa dihapus karena masih digunakan.{detail}", "warning");
                return RedirectToAction(nameof(Positions), new { activeOnly });
            }

            position.is_aktif = false;
            position.diubah_pada = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("DELETE", "jabatan", position.jabatan_id.ToString(), $"Hapus jabatan {position.nama_jabatan}", cancellationToken);
            SetAlert("Jabatan berhasil dinonaktifkan.");
            return RedirectToAction(nameof(Positions), new { activeOnly });
        }

        [HttpGet]
        public async Task<IActionResult> DepartmentsByCompany(int companyId, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!scope.IsOwner && scope.CompanyId > 0 && companyId != scope.CompanyId)
            {
                return Json(Array.Empty<object>());
            }

            var query = _context.tbl_m_departemen.AsNoTracking()
                .Where(d => d.perusahaan_id == companyId && d.is_aktif == true);
            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                query = query.Where(d => d.departemen_id == scope.DepartmentId.Value);
            }

            var departments = await query
                .OrderBy(d => d.nama_departemen)
                .Select(d => new { id = d.departemen_id, name = d.nama_departemen })
                .ToListAsync(cancellationToken);

            return Json(departments);
        }

        [HttpGet]
        public async Task<IActionResult> SectionsByCompany(int companyId, CancellationToken cancellationToken)
        {
            var scope = await BuildOrgScopeAsync(cancellationToken);
            if (!scope.IsOwner && scope.CompanyId > 0 && companyId != scope.CompanyId)
            {
                return Json(Array.Empty<object>());
            }

            var query = _context.tbl_m_seksi.AsNoTracking()
                .Where(s => s.perusahaan_id == companyId && s.is_aktif == true);
            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                query = query.Where(s => s.departemen_id == scope.DepartmentId.Value);
            }
            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                query = query.Where(s => s.seksi_id == scope.SectionId.Value);
            }

            var sections = await query
                .OrderBy(s => s.nama_seksi)
                .Select(s => new { id = s.seksi_id, name = s.nama_seksi })
                .ToListAsync(cancellationToken);

            return Json(sections);
        }

        private async Task PopulateCompanyOptionsAsync(CompanyEditViewModel model, CancellationToken cancellationToken)
        {
            model.CompanyTypeOptions = await _context.tbl_m_tipe_perusahaan.AsNoTracking()
                .OrderBy(t => t.level_urut)
                .Select(t => new SelectListItem(t.nama_tipe, t.tipe_perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);

            model.ParentCompanyOptions = await _context.tbl_m_perusahaan.AsNoTracking()
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);
        }

        private async Task PopulateDepartmentOptionsAsync(DepartmentEditViewModel model, OrgAccessScope scope, CancellationToken cancellationToken)
        {
            var companyQuery = ApplyCompanyScope(_context.tbl_m_perusahaan.AsNoTracking().AsQueryable(), scope);
            model.CompanyOptions = await companyQuery
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);
        }

        private async Task PopulateSectionOptionsAsync(SectionEditViewModel model, OrgAccessScope scope, CancellationToken cancellationToken)
        {
            var companyQuery = ApplyCompanyScope(_context.tbl_m_perusahaan.AsNoTracking().AsQueryable(), scope);
            model.CompanyOptions = await companyQuery
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);

            var deptQuery = ApplyDepartmentScope(_context.tbl_m_departemen.AsNoTracking().AsQueryable(), scope);
            model.DepartmentOptions = await deptQuery
                .OrderBy(d => d.nama_departemen)
                .Select(d => new SelectListItem(d.nama_departemen, d.departemen_id.ToString()))
                .ToListAsync(cancellationToken);
        }

        private async Task PopulatePositionOptionsAsync(PositionEditViewModel model, OrgAccessScope scope, CancellationToken cancellationToken)
        {
            var companyQuery = ApplyCompanyScope(_context.tbl_m_perusahaan.AsNoTracking().AsQueryable(), scope);
            model.CompanyOptions = await companyQuery
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);

            var sectionQuery = ApplySectionScope(_context.tbl_m_seksi.AsNoTracking().AsQueryable(), scope);
            model.SectionOptions = await sectionQuery
                .OrderBy(s => s.nama_seksi)
                .Select(s => new SelectListItem(s.nama_seksi, s.seksi_id.ToString()))
                .ToListAsync(cancellationToken);
        }

        private void ApplyDepartmentScopeToModel(DepartmentEditViewModel model, OrgAccessScope scope)
        {
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                model.CompanyId = scope.CompanyId;
            }
        }

        private void ApplySectionScopeToModel(SectionEditViewModel model, OrgAccessScope scope)
        {
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                model.CompanyId = scope.CompanyId;
            }

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                model.DepartmentId = scope.DepartmentId.Value;
            }
        }

        private void ApplyPositionScopeToModel(PositionEditViewModel model, OrgAccessScope scope)
        {
            if (!scope.IsOwner && scope.CompanyId > 0)
            {
                model.CompanyId = scope.CompanyId;
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                model.SectionId = scope.SectionId.Value;
            }
        }

        private void SetAlert(string message, string type = "success")
        {
            TempData["AlertMessage"] = message;
            TempData["AlertType"] = type;
        }

        private async Task<OrgAccessScope> BuildOrgScopeAsync(CancellationToken cancellationToken)
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

            var isTopLevel = !(departmentId.HasValue && departmentId.Value > 0)
                             && !(sectionId.HasValue && sectionId.Value > 0)
                             && !(positionId.HasValue && positionId.Value > 0);
            var isOwner = (IsOwner() || roleLevel >= 4) && isTopLevel;
            var isDepartmentAdmin = departmentId.HasValue && departmentId.Value > 0;
            var allowedCompanyIds = await _companyHierarchyService.GetDescendantCompanyIdsAsync(companyId, cancellationToken);

            return new OrgAccessScope
            {
                RoleId = roleId,
                RoleLevel = roleLevel,
                CompanyId = companyId,
                DepartmentId = departmentId,
                SectionId = sectionId,
                PositionId = positionId,
                IsOwner = isOwner,
                IsDepartmentAdmin = isDepartmentAdmin,
                AllowedCompanyIds = allowedCompanyIds
            };
        }

        private static IQueryable<Models.Db.tbl_m_perusahaan> ApplyCompanyScope(IQueryable<Models.Db.tbl_m_perusahaan> query, OrgAccessScope scope)
        {
            if (scope.AllowedCompanyIds.Count > 0)
            {
                return query.Where(c => scope.AllowedCompanyIds.Contains(c.perusahaan_id));
            }

            if (scope.IsOwner)
            {
                return query;
            }

            return query.Where(c => false);
        }

        private static IQueryable<Models.Db.tbl_m_departemen> ApplyDepartmentScope(IQueryable<Models.Db.tbl_m_departemen> query, OrgAccessScope scope)
        {
            if (scope.DepartmentId.HasValue && scope.DepartmentId.Value > 0)
            {
                return query.Where(d => d.departemen_id == scope.DepartmentId.Value);
            }

            if (scope.AllowedCompanyIds.Count > 0)
            {
                return query.Where(d => d.perusahaan_id.HasValue && scope.AllowedCompanyIds.Contains(d.perusahaan_id.Value));
            }

            if (scope.IsOwner)
            {
                return query;
            }

            return query.Where(d => false);
        }

        private static IQueryable<Models.Db.tbl_m_seksi> ApplySectionScope(IQueryable<Models.Db.tbl_m_seksi> query, OrgAccessScope scope)
        {
            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                return query.Where(s => s.seksi_id == scope.SectionId.Value);
            }

            if (scope.DepartmentId.HasValue && scope.DepartmentId.Value > 0)
            {
                return query.Where(s => s.departemen_id == scope.DepartmentId.Value);
            }

            if (scope.AllowedCompanyIds.Count > 0)
            {
                return query.Where(s => s.perusahaan_id.HasValue && scope.AllowedCompanyIds.Contains(s.perusahaan_id.Value));
            }

            if (scope.IsOwner)
            {
                return query;
            }

            return query.Where(s => false);
        }

        private IQueryable<Models.Db.tbl_m_jabatan> ApplyPositionScope(IQueryable<Models.Db.tbl_m_jabatan> query, OrgAccessScope scope)
        {
            if (scope.PositionId.HasValue && scope.PositionId.Value > 0)
            {
                return query.Where(p => p.jabatan_id == scope.PositionId.Value);
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                return query.Where(p => p.seksi_id == scope.SectionId.Value);
            }

            if (scope.DepartmentId.HasValue && scope.DepartmentId.Value > 0)
            {
                return query.Where(p => _context.tbl_m_seksi
                    .Any(s => s.seksi_id == p.seksi_id && s.departemen_id == scope.DepartmentId.Value));
            }

            if (scope.AllowedCompanyIds.Count > 0)
            {
                return query.Where(p => p.perusahaan_id.HasValue && scope.AllowedCompanyIds.Contains(p.perusahaan_id.Value));
            }

            if (scope.IsOwner)
            {
                return query;
            }

            return query.Where(p => false);
        }

        private bool IsOwner()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            return string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase);
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

        private async Task<string> BuildScopeInfoAsync(OrgAccessScope scope, CancellationToken cancellationToken)
        {
            if (scope.IsOwner)
            {
                return "Scope: Owner - seluruh hirarki perusahaan";
            }

            string? companyName = null;
            string? departmentName = null;
            string? sectionName = null;
            string? positionName = null;

            if (scope.CompanyId > 0)
            {
                companyName = await _context.tbl_m_perusahaan.AsNoTracking()
                    .Where(c => c.perusahaan_id == scope.CompanyId)
                    .Select(c => c.nama_perusahaan)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (scope.DepartmentId.HasValue && scope.DepartmentId.Value > 0)
            {
                departmentName = await _context.tbl_m_departemen.AsNoTracking()
                    .Where(d => d.departemen_id == scope.DepartmentId.Value)
                    .Select(d => d.nama_departemen)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (scope.SectionId.HasValue && scope.SectionId.Value > 0)
            {
                sectionName = await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.seksi_id == scope.SectionId.Value)
                    .Select(s => s.nama_seksi)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (scope.PositionId.HasValue && scope.PositionId.Value > 0)
            {
                positionName = await _context.tbl_m_jabatan.AsNoTracking()
                    .Where(p => p.jabatan_id == scope.PositionId.Value)
                    .Select(p => p.nama_jabatan)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var parts = new[] { companyName, departmentName, sectionName, positionName }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (parts.Count == 0)
            {
                return "Scope: Terbatas";
            }

            return $"Scope: {string.Join(" / ", parts)}";
        }

        private class OrgAccessScope
        {
            public int RoleId { get; set; }
            public int RoleLevel { get; set; }
            public int CompanyId { get; set; }
            public int? DepartmentId { get; set; }
            public int? SectionId { get; set; }
            public int? PositionId { get; set; }
            public bool IsOwner { get; set; }
            public bool IsDepartmentAdmin { get; set; }
            public HashSet<int> AllowedCompanyIds { get; set; } = new HashSet<int>();
        }
    }
}
