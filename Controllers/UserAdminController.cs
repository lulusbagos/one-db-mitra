using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;

namespace one_db_mitra.Controllers
{
    [Authorize]
    public class UserAdminController : Controller
    {
        private readonly OneDbMitraContext _context;
        private readonly Services.Audit.AuditLogger _auditLogger;
        private readonly Services.Menu.MenuProfileService _menuProfileService;

        public UserAdminController(OneDbMitraContext context, Services.Audit.AuditLogger auditLogger, Services.Menu.MenuProfileService menuProfileService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _menuProfileService = menuProfileService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            var baseQuery = _context.tbl_m_pengguna.AsNoTracking().AsQueryable();
            if (activeOnly)
            {
                baseQuery = baseQuery.Where(user => user.is_aktif);
            }

            if (scope.CompanyId > 0)
            {
                baseQuery = baseQuery.Where(user => user.perusahaan_id == scope.CompanyId);
            }

            if (scope.IsDepartmentAdmin)
            {
                var allowedUserIds = await _context.tbl_r_pengguna_peran.AsNoTracking()
                    .Where(rel => rel.is_aktif && rel.peran_id == scope.RoleId)
                    .Select(rel => rel.pengguna_id)
                    .ToListAsync(cancellationToken);
                baseQuery = baseQuery.Where(user => user.departemen_id == scope.DepartmentId && allowedUserIds.Contains(user.pengguna_id));
            }
            else if (scope.RoleLevel > 0)
            {
                var allowedUserIds = await _context.tbl_r_pengguna_peran.AsNoTracking()
                    .Where(rel => rel.is_aktif && scope.AllowedRoleIds.Contains(rel.peran_id))
                    .Select(rel => rel.pengguna_id)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                baseQuery = baseQuery.Where(user => allowedUserIds.Contains(user.pengguna_id));
            }

            var onlineThreshold = DateTime.UtcNow.AddMinutes(-30);
            var onlineUserIds = await _context.tbl_r_sesi_aktif.AsNoTracking()
                .Where(s => s.is_aktif && s.last_seen >= onlineThreshold)
                .Select(s => s.pengguna_id)
                .Distinct()
                .ToListAsync(cancellationToken);
            var onlineSet = onlineUserIds.ToHashSet();

            var users = await (from user in baseQuery
                               join company in _context.tbl_m_perusahaan.AsNoTracking() on user.perusahaan_id equals company.perusahaan_id
                               join department in _context.tbl_m_departemen.AsNoTracking() on user.departemen_id equals department.departemen_id into deptJoin
                               from dept in deptJoin.DefaultIfEmpty()
                               join section in _context.tbl_m_seksi.AsNoTracking() on user.seksi_id equals section.seksi_id into sectionJoin
                               from sec in sectionJoin.DefaultIfEmpty()
                               join position in _context.tbl_m_jabatan.AsNoTracking() on user.jabatan_id equals position.jabatan_id into positionJoin
                               from pos in positionJoin.DefaultIfEmpty()
                               orderby user.pengguna_id descending
                               select new UserAdminListItem
                               {
                                   UserId = user.pengguna_id,
                                   Username = user.username ?? string.Empty,
                                   FullName = user.nama_lengkap ?? string.Empty,
                                   CompanyName = company.nama_perusahaan ?? string.Empty,
                                   RoleName = string.Empty,
                                   PrimaryRoleId = user.peran_id,
                                   DepartmentName = dept != null ? dept.nama_departemen ?? "-" : "-",
                                   SectionName = sec != null ? sec.nama_seksi ?? "-" : "-",
                                   PositionName = pos != null ? pos.nama_jabatan ?? "-" : "-",
                                   IsActive = user.is_aktif,
                                   IsOnline = onlineSet.Contains(user.pengguna_id)
                               }).ToListAsync(cancellationToken);

            var userIds = users.Select(u => u.UserId).ToList();
            var roleMap = await (from rel in _context.tbl_r_pengguna_peran.AsNoTracking()
                                 join role in _context.tbl_m_peran.AsNoTracking() on rel.peran_id equals role.peran_id
                                 where rel.is_aktif && role.is_aktif && userIds.Contains(rel.pengguna_id)
                                 select new { rel.pengguna_id, role.nama_peran })
                .ToListAsync(cancellationToken);

            var roleLookup = roleMap
                .GroupBy(x => x.pengguna_id)
                .ToDictionary(g => g.Key, g => g.Select(x => x.nama_peran).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList());

            var fallbackRoles = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => users.Select(u => u.PrimaryRoleId).Contains(r.peran_id))
                .ToDictionaryAsync(r => r.peran_id, r => r.nama_peran, cancellationToken);

            foreach (var item in users)
            {
                if (roleLookup.TryGetValue(item.UserId, out var names) && names.Count > 0)
                {
                    item.RoleName = string.Join(", ", names);
                }
                else if (fallbackRoles.TryGetValue(item.PrimaryRoleId, out var fallbackName))
                {
                    item.RoleName = fallbackName ?? "-";
                }
                else
                {
                    item.RoleName = "-";
                }
            }

            System.Collections.Generic.List<RoleListItem> roles;
            if (scope.CanManageRoles)
            {
                roles = await _context.tbl_m_peran.AsNoTracking()
                    .OrderBy(r => r.nama_peran)
                    .Select(r => new RoleListItem
                    {
                        RoleId = r.peran_id,
                        RoleName = r.nama_peran ?? string.Empty,
                        AccessLevel = r.level_akses,
                        IsActive = r.is_aktif,
                        StartupUrl = r.startup_url ?? string.Empty
                    }).ToListAsync(cancellationToken);
            }
            else
            {
                roles = new System.Collections.Generic.List<RoleListItem>();
            }

            var viewModel = new UserAdminIndexViewModel
            {
                Users = users,
                Roles = roles,
                ActiveOnly = activeOnly
            };

            ViewBag.CanManageRoles = scope.CanManageRoles;
            ViewBag.CanImpersonate = scope.RoleLevel >= 4;
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> MenuPreview(int userId, CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            var user = await _context.tbl_m_pengguna.AsNoTracking()
                .FirstOrDefaultAsync(u => u.pengguna_id == userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (!await CanManageUserAsync(user, scope, cancellationToken))
            {
                return Forbid();
            }

            var roleIds = await _context.tbl_r_pengguna_peran.AsNoTracking()
                .Where(rel => rel.pengguna_id == user.pengguna_id && rel.is_aktif)
                .Select(rel => rel.peran_id)
                .ToListAsync(cancellationToken);
            if (roleIds.Count == 0 && user.peran_id > 0)
            {
                roleIds.Add(user.peran_id);
            }

            var primaryRoleId = await GetPrimaryRoleIdAsync(roleIds, user.peran_id, cancellationToken);
            var menuScope = new Models.Menu.MenuScope
            {
                CompanyId = user.perusahaan_id,
                RoleId = primaryRoleId,
                RoleIds = roleIds,
                DepartmentId = user.departemen_id,
                SectionId = user.seksi_id,
                PositionId = user.jabatan_id
            };

            var sessionKey = $"preview:{user.pengguna_id}:{user.perusahaan_id}:{string.Join("-", roleIds)}:{user.departemen_id}:{user.seksi_id}:{user.jabatan_id}";
            var tree = await _menuProfileService.GetMenusForSessionAsync(sessionKey, menuScope, cancellationToken);

            return PartialView("~/Views/UserAdmin/_MenuPreview.cshtml", tree);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            var model = new UserEditViewModel
            {
                IsActive = true,
                CompanyId = scope.CompanyId,
                RoleId = scope.RoleId,
                DepartmentId = scope.DepartmentId,
                RoleIds = scope.RoleId > 0 ? new System.Collections.Generic.List<int> { scope.RoleId } : new System.Collections.Generic.List<int>()
            };

            await PopulateOptionsAsync(model, scope, cancellationToken);
            ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            var usernameExists = await _context.tbl_m_pengguna.AsNoTracking()
                .AnyAsync(u => u.username == model.Username, cancellationToken);
            if (usernameExists)
            {
                ModelState.AddModelError(nameof(model.Username), "Username sudah digunakan.");
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            ApplyScopeToModel(model, scope);
            NormalizeRoleSelection(model);
            if (!CanAssignRoles(model.RoleIds, scope))
            {
                SetAlert("Tidak memiliki izin untuk menambahkan role tersebut.", "warning");
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            if (!await ValidateHierarchyAsync(model, cancellationToken))
            {
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            model.RoleId = await GetPrimaryRoleIdAsync(model.RoleIds, model.RoleId, cancellationToken);
            var entity = new Models.Db.tbl_m_pengguna
            {
                username = model.Username.Trim(),
                kata_sandi = model.Password,
                nama_lengkap = model.FullName.Trim(),
                email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                perusahaan_id = model.CompanyId,
                peran_id = model.RoleId,
                departemen_id = model.DepartmentId,
                seksi_id = model.SectionId,
                jabatan_id = model.PositionId,
                is_aktif = model.IsActive,
                dibuat_pada = DateTime.UtcNow
            };

            _context.tbl_m_pengguna.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            await UpsertUserRolesAsync(entity.pengguna_id, model.RoleIds, cancellationToken);
            await QueueWelcomeEmailAsync(entity, cancellationToken);
            await _auditLogger.LogAsync("CREATE", "pengguna", entity.pengguna_id.ToString(), $"Tambah user {entity.username}", cancellationToken);
            SetAlert("User berhasil ditambahkan.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            var user = await _context.tbl_m_pengguna
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.pengguna_id == id, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (!await CanManageUserAsync(user, scope, cancellationToken))
            {
                SetAlert("Tidak memiliki akses ke user ini.", "warning");
                return RedirectToAction(nameof(Index));
            }

            var model = new UserEditViewModel
            {
                UserId = user.pengguna_id,
                Username = user.username ?? string.Empty,
                Password = user.kata_sandi ?? string.Empty,
                FullName = user.nama_lengkap ?? string.Empty,
                Email = user.email,
                CompanyId = user.perusahaan_id,
                RoleId = user.peran_id,
                DepartmentId = user.departemen_id,
                SectionId = user.seksi_id,
                PositionId = user.jabatan_id,
                IsActive = user.is_aktif
            };

            model.RoleIds = await _context.tbl_r_pengguna_peran.AsNoTracking()
                .Where(rel => rel.pengguna_id == user.pengguna_id && rel.is_aktif)
                .Select(rel => rel.peran_id)
                .ToListAsync(cancellationToken);
            if (model.RoleIds.Count == 0 && model.RoleId > 0)
            {
                model.RoleIds.Add(model.RoleId);
            }

            await PopulateOptionsAsync(model, scope, cancellationToken);
            ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model, CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            var usernameExists = await _context.tbl_m_pengguna.AsNoTracking()
                .AnyAsync(u => u.username == model.Username && u.pengguna_id != model.UserId, cancellationToken);
            if (usernameExists)
            {
                ModelState.AddModelError(nameof(model.Username), "Username sudah digunakan.");
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            ApplyScopeToModel(model, scope);
            NormalizeRoleSelection(model);
            if (!CanAssignRoles(model.RoleIds, scope))
            {
                SetAlert("Tidak memiliki izin untuk mengubah role tersebut.", "warning");
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            if (!await ValidateHierarchyAsync(model, cancellationToken))
            {
                await PopulateOptionsAsync(model, scope, cancellationToken);
                ViewBag.IsDepartmentAdmin = scope.IsDepartmentAdmin;
                return View(model);
            }

            var user = await _context.tbl_m_pengguna
                .FirstOrDefaultAsync(u => u.pengguna_id == model.UserId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (!await CanManageUserAsync(user, scope, cancellationToken))
            {
                SetAlert("Tidak memiliki akses ke user ini.", "warning");
                return RedirectToAction(nameof(Index));
            }

            model.RoleId = await GetPrimaryRoleIdAsync(model.RoleIds, model.RoleId, cancellationToken);
            var passwordChanged = user.kata_sandi != model.Password;
            user.username = model.Username.Trim();
            user.kata_sandi = model.Password;
            user.nama_lengkap = model.FullName.Trim();
            user.email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            user.perusahaan_id = model.CompanyId;
            user.peran_id = model.RoleId;
            user.departemen_id = model.DepartmentId;
            user.seksi_id = model.SectionId;
            user.jabatan_id = model.PositionId;
            user.is_aktif = model.IsActive;
            user.diubah_pada = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await UpsertUserRolesAsync(user.pengguna_id, model.RoleIds, cancellationToken);
            if (passwordChanged)
            {
                await QueuePasswordResetEmailAsync(user, cancellationToken);
            }
            await _auditLogger.LogAsync("UPDATE", "pengguna", user.pengguna_id.ToString(), $"Ubah user {user.username}", cancellationToken);
            SetAlert("User berhasil diperbarui.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            var user = await _context.tbl_m_pengguna
                .FirstOrDefaultAsync(u => u.pengguna_id == id, cancellationToken);

            if (user is null)
            {
                SetAlert("User tidak ditemukan.", "warning");
                return RedirectToAction(nameof(Index));
            }

            if (!await CanManageUserAsync(user, scope, cancellationToken))
            {
                SetAlert("Tidak memiliki akses ke user ini.", "warning");
                return RedirectToAction(nameof(Index));
            }

            user.is_aktif = false;
            user.diubah_pada = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("DELETE", "pengguna", user.pengguna_id.ToString(), $"Hapus user {user.username}", cancellationToken);
            SetAlert("User berhasil dinonaktifkan.");
            return RedirectToAction(nameof(Index), new { activeOnly });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName, int accessLevel, bool isActive, string? startupUrl, CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            if (!scope.CanManageRoles)
            {
                SetAlert("Hanya Owner yang dapat mengelola role.", "warning");
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(roleName))
            {
                SetAlert("Nama role wajib diisi.", "warning");
                return RedirectToAction(nameof(Index));
            }

            var roleExists = await _context.tbl_m_peran.AsNoTracking()
                .AnyAsync(r => r.nama_peran == roleName.Trim(), cancellationToken);
            if (roleExists)
            {
                SetAlert("Nama role sudah digunakan.", "warning");
                return RedirectToAction(nameof(Index));
            }

            var entity = new Models.Db.tbl_m_peran
            {
                nama_peran = roleName.Trim(),
                level_akses = accessLevel,
                is_aktif = isActive,
                startup_url = NormalizeStartupUrl(startupUrl),
                dibuat_pada = DateTime.UtcNow
            };

            _context.tbl_m_peran.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("CREATE", "peran", entity.peran_id.ToString(), $"Tambah role {entity.nama_peran}", cancellationToken);
            SetAlert("Role berhasil ditambahkan.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(int roleId, string roleName, int accessLevel, bool isActive, string? startupUrl, CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            if (!scope.CanManageRoles)
            {
                SetAlert("Hanya Owner yang dapat mengelola role.", "warning");
                return RedirectToAction(nameof(Index));
            }

            var role = await _context.tbl_m_peran.FirstOrDefaultAsync(r => r.peran_id == roleId, cancellationToken);
            if (role is null)
            {
                SetAlert("Role tidak ditemukan.", "warning");
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrWhiteSpace(roleName))
            {
                var duplicate = await _context.tbl_m_peran.AsNoTracking()
                    .AnyAsync(r => r.nama_peran == roleName.Trim() && r.peran_id != roleId, cancellationToken);
                if (duplicate)
                {
                    SetAlert("Nama role sudah digunakan.", "warning");
                    return RedirectToAction(nameof(Index));
                }
                role.nama_peran = roleName.Trim();
            }
            role.level_akses = accessLevel;
            role.is_aktif = isActive;
            role.startup_url = NormalizeStartupUrl(startupUrl);
            role.diubah_pada = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("UPDATE", "peran", role.peran_id.ToString(), $"Ubah role {role.nama_peran}", cancellationToken);
            SetAlert("Role berhasil diperbarui.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(int roleId, CancellationToken cancellationToken)
        {
            var scope = await BuildAccessScopeAsync(cancellationToken);
            if (!scope.CanManageRoles)
            {
                SetAlert("Hanya Owner yang dapat mengelola role.", "warning");
                return RedirectToAction(nameof(Index));
            }

            var role = await _context.tbl_m_peran.FirstOrDefaultAsync(r => r.peran_id == roleId, cancellationToken);
            if (role is null)
            {
                SetAlert("Role tidak ditemukan.", "warning");
                return RedirectToAction(nameof(Index));
            }

            var inUse = await _context.tbl_m_pengguna.AsNoTracking()
                .AnyAsync(u => u.peran_id == roleId && u.is_aktif, cancellationToken);
            if (inUse)
            {
                SetAlert("Role masih digunakan oleh user aktif.", "warning");
                return RedirectToAction(nameof(Index));
            }

            role.is_aktif = false;
            role.diubah_pada = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync("DELETE", "peran", role.peran_id.ToString(), $"Hapus role {role.nama_peran}", cancellationToken);
            SetAlert("Role berhasil dinonaktifkan.");
            return RedirectToAction(nameof(Index));
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

        private async Task PopulateOptionsAsync(UserEditViewModel model, UserAccessScope scope, CancellationToken cancellationToken)
        {
            var companies = await _context.tbl_m_perusahaan.AsNoTracking()
                .Where(c => (c.is_aktif || c.perusahaan_id == model.CompanyId) && (scope.CompanyId <= 0 || c.perusahaan_id == scope.CompanyId))
                .OrderBy(c => c.nama_perusahaan)
                .Select(c => new SelectListItem(c.nama_perusahaan, c.perusahaan_id.ToString()))
                .ToListAsync(cancellationToken);

            var roles = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => (r.is_aktif || r.peran_id == model.RoleId) && (scope.IsDepartmentAdmin ? r.peran_id == scope.RoleId : r.level_akses <= scope.RoleLevel))
                .OrderBy(r => r.nama_peran)
                .Select(r => new SelectListItem(r.nama_peran, r.peran_id.ToString()))
                .ToListAsync(cancellationToken);

            var departments = await _context.tbl_m_departemen.AsNoTracking()
                .Where(d => (d.is_aktif || d.departemen_id == model.DepartmentId) && (scope.IsDepartmentAdmin ? d.departemen_id == scope.DepartmentId : (scope.CompanyId <= 0 || d.perusahaan_id == scope.CompanyId)))
                .OrderBy(d => d.nama_departemen)
                .Select(d => new SelectListItem(d.nama_departemen, d.departemen_id.ToString()))
                .ToListAsync(cancellationToken);

            var sections = await _context.tbl_m_seksi.AsNoTracking()
                .Where(s => (s.is_aktif || s.seksi_id == model.SectionId) && (!scope.IsDepartmentAdmin || s.departemen_id == scope.DepartmentId))
                .OrderBy(s => s.nama_seksi)
                .Select(s => new SelectListItem(s.nama_seksi, s.seksi_id.ToString()))
                .ToListAsync(cancellationToken);

            var positionsQuery = _context.tbl_m_jabatan.AsNoTracking()
                .Where(p => p.is_aktif || p.jabatan_id == model.PositionId);

            if (scope.IsDepartmentAdmin && scope.DepartmentId.HasValue)
            {
                var sectionIds = _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.departemen_id == scope.DepartmentId.Value)
                    .Select(s => s.seksi_id);
                positionsQuery = positionsQuery.Where(p => sectionIds.Contains(p.seksi_id));
            }
            else if (scope.CompanyId > 0)
            {
                positionsQuery = positionsQuery.Where(p => p.perusahaan_id == scope.CompanyId);
            }

            var positions = await positionsQuery
                .OrderBy(p => p.nama_jabatan)
                .Select(p => new SelectListItem(p.nama_jabatan, p.jabatan_id.ToString()))
                .ToListAsync(cancellationToken);

            model.CompanyOptions = companies;
            model.RoleOptions = roles;
            model.DepartmentOptions = departments;
            model.SectionOptions = sections;
            model.PositionOptions = positions;
        }

        private async Task<UserAccessScope> BuildAccessScopeAsync(CancellationToken cancellationToken)
        {
            var userId = GetClaimInt(System.Security.Claims.ClaimTypes.NameIdentifier);
            var roleId = GetClaimInt("role_id");
            var companyId = GetClaimInt("company_id");
            var departmentId = GetOptionalClaimInt("department_id");

            var roleLevel = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);

            var isDepartmentAdmin = departmentId.HasValue && departmentId.Value > 0;

            HashSet<int> allowedRoleIds;
            if (roleId > 0 && !isDepartmentAdmin)
            {
                allowedRoleIds = await _context.tbl_m_peran.AsNoTracking()
                    .Where(r => r.level_akses <= roleLevel)
                    .Select(r => r.peran_id)
                    .ToHashSetAsync(cancellationToken);
            }
            else if (roleId > 0)
            {
                allowedRoleIds = new HashSet<int> { roleId };
            }
            else
            {
                allowedRoleIds = new HashSet<int>();
            }

            return new UserAccessScope
            {
                UserId = userId,
                RoleId = roleId,
                RoleLevel = roleLevel,
                CompanyId = companyId,
                DepartmentId = departmentId,
                IsDepartmentAdmin = isDepartmentAdmin,
                CanManageRoles = roleLevel >= 4,
                AllowedRoleIds = allowedRoleIds
            };
        }

        private void ApplyScopeToModel(UserEditViewModel model, UserAccessScope scope)
        {
            if (scope.CompanyId > 0)
            {
                model.CompanyId = scope.CompanyId;
            }

            if (scope.IsDepartmentAdmin)
            {
                model.RoleId = scope.RoleId;
                model.DepartmentId = scope.DepartmentId;
            }
        }

        private void NormalizeRoleSelection(UserEditViewModel model)
        {
            if (model.RoleIds == null)
            {
                model.RoleIds = new System.Collections.Generic.List<int>();
            }

            if (model.RoleIds.Count == 0 && model.RoleId > 0)
            {
                model.RoleIds.Add(model.RoleId);
            }

            model.RoleIds = model.RoleIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (model.RoleIds.Count == 0 && model.RoleId > 0)
            {
                model.RoleIds.Add(model.RoleId);
            }

            model.RoleId = model.RoleIds.FirstOrDefault();
        }

        private bool CanAssignRoles(System.Collections.Generic.IReadOnlyList<int> roleIds, UserAccessScope scope)
        {
            if (roleIds.Count == 0)
            {
                return false;
            }

            if (scope.IsDepartmentAdmin)
            {
                return roleIds.All(id => id == scope.RoleId);
            }

            return roleIds.All(id => scope.AllowedRoleIds.Contains(id));
        }

        private async Task<bool> CanManageUserAsync(Models.Db.tbl_m_pengguna user, UserAccessScope scope, CancellationToken cancellationToken)
        {
            if (scope.CompanyId > 0 && user.perusahaan_id != scope.CompanyId)
            {
                return false;
            }

            if (scope.IsDepartmentAdmin)
            {
                if (user.departemen_id != scope.DepartmentId)
                {
                    return false;
                }

                return await _context.tbl_r_pengguna_peran.AsNoTracking()
                    .AnyAsync(rel => rel.pengguna_id == user.pengguna_id && rel.peran_id == scope.RoleId && rel.is_aktif, cancellationToken);
            }

            return await _context.tbl_r_pengguna_peran.AsNoTracking()
                .AnyAsync(rel => rel.pengguna_id == user.pengguna_id && rel.is_aktif && scope.AllowedRoleIds.Contains(rel.peran_id), cancellationToken);
        }

        private async Task<int> GetPrimaryRoleIdAsync(System.Collections.Generic.IReadOnlyList<int> roleIds, int fallbackRoleId, CancellationToken cancellationToken)
        {
            if (roleIds.Count == 0)
            {
                return fallbackRoleId;
            }

            var primary = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => roleIds.Contains(r.peran_id))
                .OrderByDescending(r => r.level_akses)
                .ThenBy(r => r.peran_id)
                .Select(r => r.peran_id)
                .FirstOrDefaultAsync(cancellationToken);

            return primary > 0 ? primary : fallbackRoleId;
        }

        private async Task UpsertUserRolesAsync(int userId, System.Collections.Generic.IReadOnlyList<int> roleIds, CancellationToken cancellationToken)
        {
            var existing = await _context.tbl_r_pengguna_peran
                .Where(rel => rel.pengguna_id == userId)
                .ToListAsync(cancellationToken);

            foreach (var rel in existing)
            {
                rel.is_aktif = false;
                rel.diubah_pada = DateTime.UtcNow;
            }

            foreach (var roleId in roleIds.Distinct())
            {
                var existingRel = existing.FirstOrDefault(rel => rel.peran_id == roleId);
                if (existingRel != null)
                {
                    existingRel.is_aktif = true;
                    existingRel.diubah_pada = DateTime.UtcNow;
                }
                else
                {
                    _context.tbl_r_pengguna_peran.Add(new Models.Db.tbl_r_pengguna_peran
                    {
                        pengguna_id = userId,
                        peran_id = roleId,
                        is_aktif = true,
                        dibuat_pada = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
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

        private class UserAccessScope
        {
            public int UserId { get; set; }
            public int RoleId { get; set; }
            public int RoleLevel { get; set; }
            public int CompanyId { get; set; }
            public int? DepartmentId { get; set; }
            public bool IsDepartmentAdmin { get; set; }
            public bool CanManageRoles { get; set; }
            public HashSet<int> AllowedRoleIds { get; set; } = new HashSet<int>();
        }

        private async Task<bool> ValidateHierarchyAsync(UserEditViewModel model, CancellationToken cancellationToken)
        {
            if (model.DepartmentId is null && (model.SectionId is not null || model.PositionId is not null))
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Departemen harus dipilih jika section atau jabatan diisi.");
                return false;
            }

            if (model.SectionId is null && model.PositionId is not null)
            {
                ModelState.AddModelError(nameof(model.SectionId), "Section harus dipilih jika jabatan diisi.");
                return false;
            }

            if (model.SectionId is not null)
            {
                var sectionValid = await _context.tbl_m_seksi.AsNoTracking()
                    .AnyAsync(s => s.seksi_id == model.SectionId && s.departemen_id == model.DepartmentId, cancellationToken);

                if (!sectionValid)
                {
                    ModelState.AddModelError(nameof(model.SectionId), "Section tidak sesuai dengan departemen.");
                    return false;
                }
            }

            if (model.PositionId is not null)
            {
                var positionValid = await _context.tbl_m_jabatan.AsNoTracking()
                    .AnyAsync(p => p.jabatan_id == model.PositionId && p.seksi_id == model.SectionId, cancellationToken);

                if (!positionValid)
                {
                    ModelState.AddModelError(nameof(model.PositionId), "Jabatan tidak sesuai dengan section.");
                    return false;
                }
            }

            return true;
        }

        private void SetAlert(string message, string type = "success")
        {
            TempData["AlertMessage"] = message;
            TempData["AlertType"] = type;
        }

        private static string? NormalizeStartupUrl(string? startupUrl)
        {
            if (string.IsNullOrWhiteSpace(startupUrl))
            {
                return null;
            }

            var trimmed = startupUrl.Trim();
            if (trimmed.StartsWith("/") || trimmed.StartsWith("#"))
            {
                return trimmed;
            }

            return $"/{trimmed}";
        }

        private async Task QueueWelcomeEmailAsync(Models.Db.tbl_m_pengguna user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(user.email))
            {
                return;
            }

            var roleNames = await (from rel in _context.tbl_r_pengguna_peran.AsNoTracking()
                                   join role in _context.tbl_m_peran.AsNoTracking() on rel.peran_id equals role.peran_id
                                   where rel.pengguna_id == user.pengguna_id && rel.is_aktif && role.is_aktif
                                   select role.nama_peran)
                .Where(name => name != null)
                .ToListAsync(cancellationToken);

            if (roleNames.Count == 0)
            {
                var fallback = await _context.tbl_m_peran.AsNoTracking()
                    .Where(r => r.peran_id == user.peran_id)
                    .Select(r => r.nama_peran)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    roleNames.Add(fallback);
                }
            }

            var roleName = roleNames.Count > 0 ? string.Join(", ", roleNames) : "-";

            var companyName = await _context.tbl_m_perusahaan.AsNoTracking()
                .Where(c => c.perusahaan_id == user.perusahaan_id)
                .Select(c => c.nama_perusahaan)
                .FirstOrDefaultAsync(cancellationToken) ?? "-";

            var departmentName = user.departemen_id.HasValue
                ? await _context.tbl_m_departemen.AsNoTracking()
                    .Where(d => d.departemen_id == user.departemen_id)
                    .Select(d => d.nama_departemen)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var sectionName = user.seksi_id.HasValue
                ? await _context.tbl_m_seksi.AsNoTracking()
                    .Where(s => s.seksi_id == user.seksi_id)
                    .Select(s => s.nama_seksi)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var positionName = user.jabatan_id.HasValue
                ? await _context.tbl_m_jabatan.AsNoTracking()
                    .Where(j => j.jabatan_id == user.jabatan_id)
                    .Select(j => j.nama_jabatan)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var loginUrl = $"{baseUrl}/Account/Login";

            var message = $@"
                <div style=""font-family: 'Segoe UI', Arial, sans-serif; color: #1f2937;"">
                    <h2 style=""margin:0 0 12px;"">Akun Anda sudah dibuat</h2>
                    <p>Halo <strong>{System.Net.WebUtility.HtmlEncode(user.nama_lengkap ?? user.username ?? "-")}</strong>,</p>
                    <p>Berikut detail akses Anda:</p>
                    <ul>
                        <li><strong>Username:</strong> {System.Net.WebUtility.HtmlEncode(user.username ?? "-")}</li>
                        <li><strong>Password:</strong> {System.Net.WebUtility.HtmlEncode(user.kata_sandi ?? "-")}</li>
                        <li><strong>Role:</strong> {System.Net.WebUtility.HtmlEncode(roleName ?? "-")}</li>
                        <li><strong>Perusahaan:</strong> {System.Net.WebUtility.HtmlEncode(companyName ?? "-")}</li>
                        <li><strong>Departemen:</strong> {System.Net.WebUtility.HtmlEncode(departmentName ?? "-")}</li>
                        <li><strong>Section:</strong> {System.Net.WebUtility.HtmlEncode(sectionName ?? "-")}</li>
                        <li><strong>Jabatan:</strong> {System.Net.WebUtility.HtmlEncode(positionName ?? "-")}</li>
                    </ul>
                    <p>Silakan login melalui tautan berikut:</p>
                    <p><a href=""{loginUrl}"">{loginUrl}</a></p>
                    <p style=""color:#6b7280; font-size:12px;"">Email ini tercatat otomatis oleh sistem.</p>
                </div>";

            var emailLog = new Models.Db.tbl_m_email_notifikasi
            {
                id = Guid.NewGuid().ToString("N"),
                email_to = user.email.Trim(),
                subject = "Akun ONE DB MITRA - Detail Login",
                pesan_html = message,
                status = "queued",
                created_at = DateTime.UtcNow,
                created_by = User.Identity?.Name ?? "system"
            };

            _context.tbl_m_email_notifikasi.Add(emailLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task QueuePasswordResetEmailAsync(Models.Db.tbl_m_pengguna user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(user.email))
            {
                return;
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var loginUrl = $"{baseUrl}/Account/Login";
            var resetBy = User.Identity?.Name ?? "system";

            var message = $@"
                <div style=""font-family: 'Segoe UI', Arial, sans-serif; color: #1f2937;"">
                    <h2 style=""margin:0 0 12px;"">Password Anda telah direset</h2>
                    <p>Halo <strong>{System.Net.WebUtility.HtmlEncode(user.nama_lengkap ?? user.username ?? "-")}</strong>,</p>
                    <p>Password akun Anda telah direset oleh <strong>{System.Net.WebUtility.HtmlEncode(resetBy)}</strong>.</p>
                    <ul>
                        <li><strong>Username:</strong> {System.Net.WebUtility.HtmlEncode(user.username ?? "-")}</li>
                        <li><strong>Password baru:</strong> {System.Net.WebUtility.HtmlEncode(user.kata_sandi ?? "-")}</li>
                    </ul>
                    <p>Silakan login melalui tautan berikut:</p>
                    <p><a href=""{loginUrl}"">{loginUrl}</a></p>
                    <p style=""color:#6b7280; font-size:12px;"">Email ini tercatat otomatis oleh sistem.</p>
                </div>";

            var emailLog = new Models.Db.tbl_m_email_notifikasi
            {
                id = Guid.NewGuid().ToString("N"),
                email_to = user.email.Trim(),
                subject = "Reset Password - ONE DB MITRA",
                pesan_html = message,
                status = "queued",
                created_at = DateTime.UtcNow,
                created_by = resetBy
            };

            _context.tbl_m_email_notifikasi.Add(emailLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
