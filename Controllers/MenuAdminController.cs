using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using one_db_mitra.Models.Menu;
using one_db_mitra.Services.Menu;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;

namespace one_db_mitra.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class MenuAdminController : Controller
    {
        private readonly MenuProfileService _menuProfileService;
        private readonly IMenuRepository _menuRepository;
        private readonly OneDbMitraContext _context;
        private readonly Services.Audit.AuditLogger _auditLogger;

        public MenuAdminController(MenuProfileService menuProfileService, IMenuRepository menuRepository, OneDbMitraContext context, Services.Audit.AuditLogger auditLogger)
        {
            _menuProfileService = menuProfileService;
            _menuRepository = menuRepository;
            _context = context;
            _auditLogger = auditLogger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? notification = null, CancellationToken cancellationToken = default)
        {
            var viewModel = await BuildViewModelAsync(notification, cancellationToken);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Manage(bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var baseQuery = _context.tbl_m_menu.AsNoTracking().AsQueryable();
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(menu => menu.is_aktif);
            }

            var menus = await (from menu in baseQuery
                               join parent in _context.tbl_m_menu.AsNoTracking() on menu.menu_induk_id equals parent.menu_id into parentJoin
                               from parent in parentJoin.DefaultIfEmpty()
                               orderby menu.menu_id descending
                               select new MenuManageItem
                               {
                                   MenuId = menu.menu_id,
                                   ParentMenuId = menu.menu_induk_id,
                                   MenuCode = menu.kode_menu ?? string.Empty,
                                   DisplayName = menu.nama_tampil ?? string.Empty,
                                   ParentName = parent != null ? parent.nama_tampil ?? "-" : "-",
                                   IconKey = menu.ikon ?? string.Empty,
                                   UrlPath = menu.url ?? string.Empty,
                                   SortOrder = menu.urutan,
                                   IsHidden = menu.sembunyikan,
                                   OpenInNewTab = menu.tab_baru,
                                   IsActive = menu.is_aktif
                               }).ToListAsync(cancellationToken);

            var parentOptions = await BuildParentOptionsAsync(cancellationToken);

            var model = new MenuManageViewModel
            {
                Menus = menus,
                ParentOptions = parentOptions,
                ActiveOnly = activeOnly
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> RoleAccess(int? roleId, CancellationToken cancellationToken)
        {
            var roles = await _context.tbl_m_peran.AsNoTracking()
                .OrderBy(r => r.nama_peran)
                .ToListAsync(cancellationToken);

            if (!roles.Any())
            {
                return View(new RoleMenuViewModel());
            }

            var selectedRoleId = roleId ?? roles.First().peran_id;
            var selectedRole = roles.FirstOrDefault(r => r.peran_id == selectedRoleId) ?? roles.First();

            var roleOptions = roles
                .Select(r => new SelectListItem(r.nama_peran, r.peran_id.ToString(), r.peran_id == selectedRole.peran_id))
                .ToList();

            var menus = await _context.tbl_m_menu.AsNoTracking()
                .OrderBy(m => m.urutan)
                .ThenBy(m => m.nama_tampil)
                .ToListAsync(cancellationToken);

            var selectedMenuIds = await _context.tbl_r_menu_peran.AsNoTracking()
                .Where(r => r.peran_id == selectedRole.peran_id)
                .Select(r => r.menu_id)
                .ToListAsync(cancellationToken);

            var flatMenus = BuildMenuAccessItems(menus);

            var model = new RoleMenuViewModel
            {
                RoleId = selectedRole.peran_id,
                RoleName = selectedRole.nama_peran ?? string.Empty,
                RoleOptions = roleOptions,
                Menus = flatMenus,
                SelectedMenuIds = selectedMenuIds.ToHashSet()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleAccess(RoleMenuViewModel model, int[] selectedMenus, CancellationToken cancellationToken)
        {
            if (model.RoleId <= 0)
            {
                return RedirectToAction(nameof(RoleAccess));
            }

            var existing = _context.tbl_r_menu_peran.Where(r => r.peran_id == model.RoleId);
            _context.tbl_r_menu_peran.RemoveRange(existing);

            foreach (var menuId in selectedMenus.Distinct())
            {
                _context.tbl_r_menu_peran.Add(new Models.Db.tbl_r_menu_peran
                {
                    menu_id = menuId,
                    peran_id = model.RoleId,
                    is_aktif = true,
                    dibuat_pada = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());

            return RedirectToAction(nameof(RoleAccess), new { roleId = model.RoleId });
        }

        [HttpGet]
        public async Task<IActionResult> CompanyAccess(int? companyId, CancellationToken cancellationToken)
        {
            var companies = await _context.tbl_m_perusahaan.AsNoTracking()
                .OrderBy(c => c.nama_perusahaan)
                .ToListAsync(cancellationToken);

            if (!companies.Any())
            {
                return View(new CompanyMenuViewModel());
            }

            var selectedCompanyId = companyId ?? companies.First().perusahaan_id;
            var selectedCompany = companies.FirstOrDefault(c => c.perusahaan_id == selectedCompanyId) ?? companies.First();

            var companyOptions = companies
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString(), c.perusahaan_id == selectedCompany.perusahaan_id))
                .ToList();

            var menus = await _context.tbl_m_menu.AsNoTracking()
                .OrderBy(m => m.urutan)
                .ThenBy(m => m.nama_tampil)
                .ToListAsync(cancellationToken);

            var selectedMenuIds = await _context.tbl_r_menu_perusahaan.AsNoTracking()
                .Where(r => r.perusahaan_id == selectedCompany.perusahaan_id)
                .Select(r => r.menu_id)
                .ToListAsync(cancellationToken);

            var flatMenus = BuildMenuAccessItems(menus);

            var model = new CompanyMenuViewModel
            {
                CompanyId = selectedCompany.perusahaan_id,
                CompanyName = selectedCompany.nama_perusahaan ?? string.Empty,
                CompanyOptions = companyOptions,
                Menus = flatMenus,
                SelectedMenuIds = selectedMenuIds.ToHashSet()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompanyAccess(CompanyMenuViewModel model, int[] selectedMenus, CancellationToken cancellationToken)
        {
            if (model.CompanyId <= 0)
            {
                return RedirectToAction(nameof(CompanyAccess));
            }

            var existing = _context.tbl_r_menu_perusahaan.Where(r => r.perusahaan_id == model.CompanyId);
            _context.tbl_r_menu_perusahaan.RemoveRange(existing);

            foreach (var menuId in selectedMenus.Distinct())
            {
                _context.tbl_r_menu_perusahaan.Add(new Models.Db.tbl_r_menu_perusahaan
                {
                    menu_id = menuId,
                    perusahaan_id = model.CompanyId,
                    is_aktif = true,
                    dibuat_pada = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());

            return RedirectToAction(nameof(CompanyAccess), new { companyId = model.CompanyId });
        }

        [HttpGet]
        public async Task<IActionResult> AccessMatrix(int? roleId, int? companyId, CancellationToken cancellationToken)
        {
            var roles = await _context.tbl_m_peran.AsNoTracking().OrderBy(r => r.nama_peran).ToListAsync(cancellationToken);
            var companies = await _context.tbl_m_perusahaan.AsNoTracking().OrderBy(c => c.nama_perusahaan).ToListAsync(cancellationToken);

            if (!roles.Any() || !companies.Any())
            {
                return View(new MenuAccessMatrixViewModel());
            }

            var selectedRoleId = roleId ?? roles.First().peran_id;
            var selectedCompanyId = companyId ?? companies.First().perusahaan_id;

            var roleOptions = roles.Select(r => new SelectListItem(r.nama_peran, r.peran_id.ToString(), r.peran_id == selectedRoleId)).ToList();
            var companyOptions = companies.Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString(), c.perusahaan_id == selectedCompanyId)).ToList();

            var menus = await _context.tbl_m_menu.AsNoTracking()
                .OrderBy(m => m.urutan)
                .ThenBy(m => m.nama_tampil)
                .ToListAsync(cancellationToken);

            var roleMenuIds = await _context.tbl_r_menu_peran.AsNoTracking()
                .Where(r => r.peran_id == selectedRoleId)
                .Select(r => r.menu_id)
                .ToListAsync(cancellationToken);

            var companyMenuIds = await _context.tbl_r_menu_perusahaan.AsNoTracking()
                .Where(r => r.perusahaan_id == selectedCompanyId)
                .Select(r => r.menu_id)
                .ToListAsync(cancellationToken);

            var model = new MenuAccessMatrixViewModel
            {
                RoleId = selectedRoleId,
                CompanyId = selectedCompanyId,
                RoleOptions = roleOptions,
                CompanyOptions = companyOptions,
                Menus = BuildMenuAccessItems(menus),
                RoleMenuIds = roleMenuIds.ToHashSet(),
                CompanyMenuIds = companyMenuIds.ToHashSet()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AccessMatrix(MenuAccessMatrixViewModel model, int[] roleMenus, int[] companyMenus, CancellationToken cancellationToken)
        {
            if (model.RoleId <= 0 || model.CompanyId <= 0)
            {
                return RedirectToAction(nameof(AccessMatrix));
            }

            var existingRole = _context.tbl_r_menu_peran.Where(r => r.peran_id == model.RoleId);
            _context.tbl_r_menu_peran.RemoveRange(existingRole);

            foreach (var menuId in roleMenus.Distinct())
            {
                _context.tbl_r_menu_peran.Add(new Models.Db.tbl_r_menu_peran
                {
                    menu_id = menuId,
                    peran_id = model.RoleId,
                    is_aktif = true,
                    dibuat_pada = DateTime.UtcNow
                });
            }

            var existingCompany = _context.tbl_r_menu_perusahaan.Where(r => r.perusahaan_id == model.CompanyId);
            _context.tbl_r_menu_perusahaan.RemoveRange(existingCompany);

            foreach (var menuId in companyMenus.Distinct())
            {
                _context.tbl_r_menu_perusahaan.Add(new Models.Db.tbl_r_menu_perusahaan
                {
                    menu_id = menuId,
                    perusahaan_id = model.CompanyId,
                    is_aktif = true,
                    dibuat_pada = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());

            return RedirectToAction(nameof(AccessMatrix), new { roleId = model.RoleId, companyId = model.CompanyId });
        }

        [HttpGet]
        public async Task<IActionResult> MenuTreePartial(CancellationToken cancellationToken)
        {
            var tree = await _menuProfileService.GetMenusForSessionAsync(BuildSessionKey(), BuildScope(), cancellationToken);
            return PartialView("~/Views/MenuAdmin/_MenuTree.cshtml", tree);
        }

        [HttpGet]
        public async Task<IActionResult> ParentMenuOptions(CancellationToken cancellationToken)
        {
            var tree = await _menuProfileService.GetMenusForSessionAsync(BuildSessionKey(), BuildScope(), cancellationToken);
            var options = BuildParentMenuOptions(tree);
            return PartialView("~/Views/MenuAdmin/_ParentMenuOptions.cshtml", options);
        }

        [HttpPost]
        public async Task<IActionResult> AddMenu(MenuFormModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = BuildModelErrorMessage() });
            }

            var result = await _menuRepository.AddMenuAsync(model.ToRequest(), cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { message = result.Message ?? "Gagal menambahkan menu." });
            }

            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());
            return Json(new { success = true, message = $"Menu '{result.Menu?.DisplayName}' berhasil ditambahkan." });
        }

        [HttpPost]
        public async Task<IActionResult> AddSubMenu(SubMenuFormModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = BuildModelErrorMessage() });
            }

            if (model.ParentMenuId is null || model.ParentMenuId <= 0)
            {
                return BadRequest(new { message = "Menu induk harus dipilih." });
            }

            var result = await _menuRepository.AddSubMenuAsync(model.ParentMenuId.Value, model.ToRequest(), cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { message = result.Message ?? "Gagal menambahkan sub menu." });
            }

            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());
            return Json(new { success = true, message = $"Sub menu '{result.Menu?.DisplayName}' berhasil ditambahkan." });
        }

        private async Task<MenuAdminViewModel> BuildViewModelAsync(string? notification, CancellationToken cancellationToken)
        {
            var tree = await _menuProfileService.GetMenusForSessionAsync(BuildSessionKey(), BuildScope(), cancellationToken);
            var options = BuildParentMenuOptions(tree);
            var counts = await LoadCountsAsync(cancellationToken);
            var health = await LoadHealthAsync(cancellationToken);
            var startupPage = await LoadStartupPageAsync(cancellationToken);
            var hierarchy = await LoadCompanyHierarchyAsync(cancellationToken);
            var roleName = User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var roleId = int.TryParse(User.FindFirstValue("role_id"), out var parsedRole) ? parsedRole : 0;
            var roleLevel = roleId > 0
                ? await _context.tbl_m_peran.AsNoTracking()
                    .Where(r => r.peran_id == roleId)
                    .Select(r => r.level_akses)
                    .FirstOrDefaultAsync(cancellationToken)
                : 0;
            var companyId = int.TryParse(User.FindFirstValue("company_id"), out var parsedCompany) ? parsedCompany : 0;
            var companyName = User.FindFirstValue("company_name") ?? "-";
            var isOwner = roleLevel >= 4 || roleName.Equals("Owner", StringComparison.OrdinalIgnoreCase);
            var activeRoleCount = await _context.tbl_m_peran.AsNoTracking()
                .CountAsync(r => r.is_aktif, cancellationToken);
            var activeKaryawanCount = isOwner
                ? await _context.tbl_t_karyawan.AsNoTracking().CountAsync(k => k.status_aktif, cancellationToken)
                : await _context.tbl_t_karyawan.AsNoTracking()
                    .CountAsync(k => k.status_aktif && k.perusahaan_id == companyId, cancellationToken);
            var blacklistCountQuery = from s in _context.tbl_r_karyawan_status.AsNoTracking()
                                      join k in _context.tbl_t_karyawan.AsNoTracking() on s.karyawan_id equals k.karyawan_id
                                      where s.status == "blacklist" && s.tanggal_selesai == null
                                      select new { s, k };
            var resignCountQuery = from s in _context.tbl_r_karyawan_status.AsNoTracking()
                                   join k in _context.tbl_t_karyawan.AsNoTracking() on s.karyawan_id equals k.karyawan_id
                                   where s.status == "nonaktif" && s.tanggal_selesai == null && s.kategori_blacklist == "Resign"
                                   select new { s, k };
            var rehireCountQuery = _context.tbl_r_karyawan_penempatan.AsNoTracking()
                .Where(p => p.status == "rehire");

            if (!isOwner && companyId > 0)
            {
                blacklistCountQuery = blacklistCountQuery.Where(x => x.k.perusahaan_id == companyId);
                resignCountQuery = resignCountQuery.Where(x => x.k.perusahaan_id == companyId);
                rehireCountQuery = rehireCountQuery.Where(p => p.perusahaan_tujuan_id == companyId);
            }

            var blacklistCount = await blacklistCountQuery.CountAsync(cancellationToken);
            var resignCount = await resignCountQuery.CountAsync(cancellationToken);
            var rehireCount = await rehireCountQuery.CountAsync(cancellationToken);
            var recentAudits = await _context.tbl_r_audit_log.AsNoTracking()
                .OrderByDescending(a => a.dibuat_pada)
                .Take(8)
                .Select(a => new Models.Menu.AuditLogItem
                {
                    Action = a.aksi ?? "-",
                    Entity = a.entitas ?? "-",
                    Description = a.deskripsi,
                    Username = a.username,
                    CreatedAt = a.dibuat_pada
                })
                .ToListAsync(cancellationToken);
            var activityHeatmap = await BuildActivityHeatmapAsync(cancellationToken);

            return new MenuAdminViewModel
            {
                MenuTree = tree,
                ParentMenuOptions = options,
                Notification = notification,
                RoleName = roleName,
                RoleLevel = roleLevel,
                CompanyName = companyName,
                ActiveKaryawanCount = activeKaryawanCount,
                ActiveRoleCount = activeRoleCount,
                BlacklistCount = blacklistCount,
                ResignCount = resignCount,
                RehireCount = rehireCount,
                ActiveMenuCount = counts.ActiveMenuCount,
                ActiveUserCount = counts.ActiveUserCount,
                ActiveCompanyCount = counts.ActiveCompanyCount,
                ActiveDepartmentCount = counts.ActiveDepartmentCount,
                MenuTrend = BuildTrend(counts.ActiveMenuCount),
                UserTrend = BuildTrend(counts.ActiveUserCount),
                CompanyTrend = BuildTrend(counts.ActiveCompanyCount),
                DepartmentTrend = BuildTrend(counts.ActiveDepartmentCount),
                Health = health,
                StartupPage = startupPage,
                CompanyHierarchy = hierarchy,
                RecentAudits = recentAudits,
                ActivityHeatmap = activityHeatmap
            };
        }

        private async Task<IReadOnlyList<ActivityDayItem>> BuildActivityHeatmapAsync(CancellationToken cancellationToken)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-27);

            var auditCounts = await _context.tbl_r_audit_log.AsNoTracking()
                .Where(a => a.dibuat_pada >= startDate)
                .GroupBy(a => a.dibuat_pada.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var sessionCounts = await _context.tbl_r_sesi_aktif.AsNoTracking()
                .Where(s => s.dibuat_pada >= startDate)
                .GroupBy(s => s.dibuat_pada.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var map = new Dictionary<DateTime, int>();
            foreach (var item in auditCounts)
            {
                map[item.Day] = item.Count;
            }

            foreach (var item in sessionCounts)
            {
                map[item.Day] = map.TryGetValue(item.Day, out var existing) ? existing + item.Count : item.Count;
            }

            var data = new List<ActivityDayItem>();
            for (var i = 0; i < 28; i++)
            {
                var day = startDate.AddDays(i);
                map.TryGetValue(day, out var count);
                data.Add(new ActivityDayItem
                {
                    Date = day,
                    Count = count
                });
            }

            return data;
        }

        private static IReadOnlyList<int> BuildTrend(int current)
        {
            var baseValue = Math.Max(1, current);
            var start = Math.Max(0, baseValue - 3);
            return new[]
            {
                start,
                Math.Max(0, baseValue - 1),
                baseValue,
                baseValue + 2,
                baseValue + 1,
                baseValue + 3
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMenu(MenuEditViewModel model, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                var viewModel = await BuildManageViewModelAsync(activeOnly, cancellationToken);
                SetAlert("Data menu belum lengkap.", "warning");
                return View("Manage", viewModel);
            }

            var code = model.MenuCode.Trim().ToUpperInvariant();
            var exists = await _context.tbl_m_menu.AsNoTracking()
                .AnyAsync(m => m.kode_menu == code, cancellationToken);
            if (exists)
            {
                var viewModel = await BuildManageViewModelAsync(activeOnly, cancellationToken);
                SetAlert("Kode menu sudah digunakan.", "warning");
                return View("Manage", viewModel);
            }

            var entity = new Models.Db.tbl_m_menu
            {
                menu_induk_id = model.ParentMenuId,
                kode_menu = code,
                nama_tampil = model.DisplayName.Trim(),
                ikon = string.IsNullOrWhiteSpace(model.IconKey) ? "bi-circle" : model.IconKey.Trim(),
                url = NormalizeMenuUrl(model.UrlPath),
                urutan = model.SortOrder,
                sembunyikan = model.IsHidden,
                tab_baru = model.OpenInNewTab,
                is_aktif = model.IsActive,
                dibuat_pada = DateTime.UtcNow
            };

            _context.tbl_m_menu.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());
            await _auditLogger.LogAsync("CREATE", "menu", entity.menu_id.ToString(), $"Tambah menu {entity.nama_tampil}", cancellationToken);
            SetAlert("Menu berhasil ditambahkan.");

            return RedirectToAction(nameof(Manage), new { activeOnly });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMenu(MenuEditViewModel model, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                var viewModel = await BuildManageViewModelAsync(activeOnly, cancellationToken);
                SetAlert("Data menu belum lengkap.", "warning");
                return View("Manage", viewModel);
            }

            var menu = await _context.tbl_m_menu.FirstOrDefaultAsync(m => m.menu_id == model.MenuId, cancellationToken);
            if (menu is null)
            {
                SetAlert("Menu tidak ditemukan.", "warning");
                return NotFound();
            }

            var code = model.MenuCode.Trim().ToUpperInvariant();
            var exists = await _context.tbl_m_menu.AsNoTracking()
                .AnyAsync(m => m.kode_menu == code && m.menu_id != model.MenuId, cancellationToken);
            if (exists)
            {
                var viewModel = await BuildManageViewModelAsync(activeOnly, cancellationToken);
                SetAlert("Kode menu sudah digunakan.", "warning");
                return View("Manage", viewModel);
            }

            menu.menu_induk_id = model.ParentMenuId;
            menu.kode_menu = code;
            menu.nama_tampil = model.DisplayName.Trim();
            menu.ikon = string.IsNullOrWhiteSpace(model.IconKey) ? "bi-circle" : model.IconKey.Trim();
            menu.url = NormalizeMenuUrl(model.UrlPath);
            menu.urutan = model.SortOrder;
            menu.sembunyikan = model.IsHidden;
            menu.tab_baru = model.OpenInNewTab;
            menu.is_aktif = model.IsActive;
            menu.diubah_pada = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());
            await _auditLogger.LogAsync("UPDATE", "menu", menu.menu_id.ToString(), $"Ubah menu {menu.nama_tampil}", cancellationToken);
            SetAlert("Menu berhasil diperbarui.");

            return RedirectToAction(nameof(Manage), new { activeOnly });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMenu(int id, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var menu = await _context.tbl_m_menu.FirstOrDefaultAsync(m => m.menu_id == id, cancellationToken);
            if (menu is null)
            {
                SetAlert("Menu tidak ditemukan.", "warning");
                return RedirectToAction(nameof(Manage), new { activeOnly });
            }

            var hasChildren = await _context.tbl_m_menu.AsNoTracking()
                .AnyAsync(m => m.menu_induk_id == id && m.is_aktif, cancellationToken);
            var hasRoleMapping = await _context.tbl_r_menu_peran.AsNoTracking()
                .AnyAsync(r => r.menu_id == id, cancellationToken);
            var hasCompanyMapping = await _context.tbl_r_menu_perusahaan.AsNoTracking()
                .AnyAsync(r => r.menu_id == id, cancellationToken);

            if (hasChildren || hasRoleMapping || hasCompanyMapping)
            {
                SetAlert("Menu tidak bisa dihapus karena masih digunakan.", "warning");
                return RedirectToAction(nameof(Manage), new { activeOnly });
            }

            menu.is_aktif = false;
            menu.diubah_pada = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            _menuProfileService.InvalidateSessionMenu(BuildSessionKey());
            await _auditLogger.LogAsync("DELETE", "menu", menu.menu_id.ToString(), $"Hapus menu {menu.nama_tampil}", cancellationToken);
            SetAlert("Menu berhasil dinonaktifkan.");

            return RedirectToAction(nameof(Manage), new { activeOnly });
        }

        private async Task<MenuManageViewModel> BuildManageViewModelAsync(bool activeOnly, CancellationToken cancellationToken)
        {
            var baseQuery = _context.tbl_m_menu.AsNoTracking().AsQueryable();
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(menu => menu.is_aktif);
            }

            var menus = await (from menu in baseQuery
                               join parent in _context.tbl_m_menu.AsNoTracking() on menu.menu_induk_id equals parent.menu_id into parentJoin
                               from parent in parentJoin.DefaultIfEmpty()
                               orderby menu.menu_id descending
                               select new MenuManageItem
                               {
                                   MenuId = menu.menu_id,
                                   ParentMenuId = menu.menu_induk_id,
                                   MenuCode = menu.kode_menu ?? string.Empty,
                                   DisplayName = menu.nama_tampil ?? string.Empty,
                                   ParentName = parent != null ? parent.nama_tampil ?? "-" : "-",
                                   IconKey = menu.ikon ?? string.Empty,
                                   UrlPath = menu.url ?? string.Empty,
                                   SortOrder = menu.urutan,
                                   IsHidden = menu.sembunyikan,
                                   OpenInNewTab = menu.tab_baru,
                                   IsActive = menu.is_aktif
                               }).ToListAsync(cancellationToken);

            var parentOptions = await BuildParentOptionsAsync(cancellationToken);

            return new MenuManageViewModel
            {
                Menus = menus,
                ParentOptions = parentOptions,
                ActiveOnly = activeOnly
            };
        }

        private async Task<IEnumerable<SelectListItem>> BuildParentOptionsAsync(CancellationToken cancellationToken)
        {
            var menus = await _context.tbl_m_menu.AsNoTracking()
                .Where(menu => menu.is_aktif)
                .OrderBy(menu => menu.urutan)
                .ThenBy(menu => menu.nama_tampil)
                .ToListAsync(cancellationToken);

            return menus.Select(menu => new SelectListItem(menu.nama_tampil, menu.menu_id.ToString())).ToList();
        }

        private static IReadOnlyList<MenuAccessItem> BuildMenuAccessItems(System.Collections.Generic.IReadOnlyList<Models.Db.tbl_m_menu> menus)
        {
            var roots = menus.Where(m => m.menu_induk_id is null).ToList();
            var byParent = menus
                .Where(m => m.menu_induk_id.HasValue)
                .GroupBy(m => m.menu_induk_id!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
            var result = new System.Collections.Generic.List<MenuAccessItem>();

            void AddChildren(int? parentId, int depth)
            {
                var items = parentId is null
                    ? roots
                    : (byParent.TryGetValue(parentId.Value, out var children) ? children : null);

                if (items is null || items.Count == 0)
                {
                    return;
                }

                foreach (var item in items.OrderBy(m => m.urutan).ThenBy(m => m.nama_tampil))
                {
                    result.Add(new MenuAccessItem
                    {
                        MenuId = item.menu_id,
                        ParentMenuId = item.menu_induk_id,
                        DisplayName = item.nama_tampil ?? string.Empty,
                        MenuCode = item.kode_menu ?? string.Empty,
                        SortOrder = item.urutan,
                        Depth = depth
                    });

                    AddChildren(item.menu_id, depth + 1);
                }
            }

            AddChildren(null, 0);
            return result;
        }

        private void SetAlert(string message, string type = "success")
        {
            TempData["AlertMessage"] = message;
            TempData["AlertType"] = type;
        }

        private static string NormalizeMenuUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "#";
            }

            var trimmed = url.Trim();
            if (trimmed.StartsWith("/") || trimmed.StartsWith("#"))
            {
                return trimmed;
            }

            return $"/{trimmed}";
        }

        private async Task<(int ActiveMenuCount, int ActiveUserCount, int ActiveCompanyCount, int ActiveDepartmentCount)> LoadCountsAsync(CancellationToken cancellationToken)
        {
            var activeMenus = await _context.tbl_m_menu.AsNoTracking().CountAsync(m => m.is_aktif, cancellationToken);
            var activeUsers = await _context.tbl_m_pengguna.AsNoTracking().CountAsync(u => u.is_aktif, cancellationToken);
            var activeCompanies = await _context.tbl_m_perusahaan.AsNoTracking().CountAsync(c => c.is_aktif, cancellationToken);
            var activeDepartments = await _context.tbl_m_departemen.AsNoTracking().CountAsync(d => d.is_aktif, cancellationToken);

            return (activeMenus, activeUsers, activeCompanies, activeDepartments);
        }

        private async Task<HealthStatusViewModel> LoadHealthAsync(CancellationToken cancellationToken)
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            var lastSync = await _context.tbl_r_audit_log.AsNoTracking()
                .Where(a => a.aksi == "SYNC")
                .OrderByDescending(a => a.audit_id)
                .Select(a => (DateTime?)a.dibuat_pada)
                .FirstOrDefaultAsync(cancellationToken);

            return new HealthStatusViewModel
            {
                IsDatabaseUp = canConnect,
                JobStatus = canConnect ? "Idle" : "Down",
                LastSyncAt = lastSync
            };
        }

        private async Task<string> LoadStartupPageAsync(CancellationToken cancellationToken)
        {
            var roleId = GetClaimInt("role_id");
            if (roleId <= 0)
            {
                return "-";
            }

            var startupUrl = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.startup_url)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(startupUrl) ? "-" : startupUrl.Trim();
        }

        private async Task<IReadOnlyList<CompanyHierarchyItem>> LoadCompanyHierarchyAsync(CancellationToken cancellationToken)
        {
            var roleId = GetClaimInt("role_id");
            if (roleId <= 0)
            {
                return Array.Empty<CompanyHierarchyItem>();
            }

            var roleLevel = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);

            if (roleLevel < 4)
            {
                return Array.Empty<CompanyHierarchyItem>();
            }

            var companies = await _context.tbl_m_perusahaan.AsNoTracking()
                .Where(c => c.is_aktif)
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new
                {
                    CompanyId = c.perusahaan_id,
                    Name = c.nama_perusahaan
                })
                .ToListAsync(cancellationToken);

            var departments = await _context.tbl_m_departemen.AsNoTracking()
                .Where(d => d.is_aktif)
                .OrderBy(d => d.nama_departemen)
                .Select(d => new
                {
                    DepartmentId = d.departemen_id,
                    CompanyId = d.perusahaan_id,
                    Name = d.nama_departemen
                })
                .ToListAsync(cancellationToken);

            var sections = await _context.tbl_m_seksi.AsNoTracking()
                .Where(s => s.is_aktif)
                .OrderBy(s => s.nama_seksi)
                .Select(s => new
                {
                    SectionId = s.seksi_id,
                    DepartmentId = s.departemen_id,
                    Name = s.nama_seksi
                })
                .ToListAsync(cancellationToken);

            var positions = await _context.tbl_m_jabatan.AsNoTracking()
                .Where(j => j.is_aktif)
                .OrderBy(j => j.nama_jabatan)
                .Select(j => new
                {
                    PositionId = j.jabatan_id,
                    SectionId = j.seksi_id,
                    Name = j.nama_jabatan
                })
                .ToListAsync(cancellationToken);

            var sectionsByDepartment = sections
                .GroupBy(s => s.DepartmentId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var positionsBySection = positions
                .Where(p => p.SectionId.HasValue)
                .GroupBy(p => p.SectionId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
            var departmentsByCompany = departments
                .GroupBy(d => d.CompanyId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<CompanyHierarchyItem>();
            foreach (var company in companies)
            {
                var deptItems = new List<DepartmentHierarchyItem>();
                if (departmentsByCompany.TryGetValue(company.CompanyId, out var deptList))
                {
                    foreach (var dept in deptList)
                    {
                        var sectionItems = new List<SectionHierarchyItem>();
                        if (sectionsByDepartment.TryGetValue(dept.DepartmentId, out var sectionList))
                        {
                            foreach (var section in sectionList)
                            {
                                var positionItems = new List<PositionHierarchyItem>();
                                if (positionsBySection.TryGetValue(section.SectionId, out var positionList))
                                {
                                    foreach (var position in positionList)
                                    {
                                        positionItems.Add(new PositionHierarchyItem
                                        {
                                            PositionId = position.PositionId,
                                            PositionName = position.Name ?? string.Empty
                                        });
                                    }
                                }

                                sectionItems.Add(new SectionHierarchyItem
                                {
                                    SectionId = section.SectionId,
                                    SectionName = section.Name ?? string.Empty,
                                    Positions = positionItems
                                });
                            }
                        }

                        deptItems.Add(new DepartmentHierarchyItem
                        {
                            DepartmentId = dept.DepartmentId,
                            DepartmentName = dept.Name ?? string.Empty,
                            Sections = sectionItems
                        });
                    }
                }

                result.Add(new CompanyHierarchyItem
                {
                    CompanyId = company.CompanyId,
                    CompanyName = company.Name ?? string.Empty,
                    Departments = deptItems
                });
            }

            return result;
        }

        private static IEnumerable<SelectListItem> BuildParentMenuOptions(IEnumerable<MenuItem> menus, string prefix = "")
        {
            var ordered = menus.OrderBy(menu => menu.SortOrder).ThenBy(menu => menu.DisplayName);
            foreach (var menu in ordered)
            {
                var label = string.IsNullOrWhiteSpace(prefix) ? menu.DisplayName : $"{prefix} / {menu.DisplayName}";
                yield return new SelectListItem(label, menu.Id.ToString());

                if (menu.Children.Any())
                {
                    foreach (var child in BuildParentMenuOptions(menu.Children, label))
                    {
                        yield return child;
                    }
                }
            }
        }

        private string BuildModelErrorMessage()
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();

            return errors.Any() ? string.Join(" ", errors) : "Input tidak valid.";
        }

        private MenuScope BuildScope()
        {
            return new MenuScope
            {
                CompanyId = GetClaimInt("company_id"),
                RoleId = GetClaimInt("role_id"),
                RoleIds = GetRoleIds(),
                DepartmentId = GetOptionalClaimInt("department_id"),
                SectionId = GetOptionalClaimInt("section_id"),
                PositionId = GetOptionalClaimInt("position_id")
            };
        }

        private string BuildSessionKey()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var companyId = GetClaimInt("company_id").ToString();
            var roleIds = string.Join("-", GetRoleIds());
            var departmentId = GetOptionalClaimInt("department_id")?.ToString() ?? "0";
            var sectionId = GetOptionalClaimInt("section_id")?.ToString() ?? "0";
            var positionId = GetOptionalClaimInt("position_id")?.ToString() ?? "0";

            return $"{userId}:{companyId}:{roleIds}:{departmentId}:{sectionId}:{positionId}".ToLowerInvariant();
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

        private IReadOnlyList<int> GetRoleIds()
        {
            var value = User.FindFirstValue("role_ids");
            if (string.IsNullOrWhiteSpace(value))
            {
                var single = GetClaimInt("role_id");
                return single > 0 ? new[] { single } : Array.Empty<int>();
            }

            return value.Split(',')
                .Select(part => int.TryParse(part.Trim(), out var parsed) ? parsed : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }
    }
}
