using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using one_db_mitra.Data;
using one_db_mitra.Models.Auth;
using one_db_mitra.Services.AppSetting;

namespace one_db_mitra.Controllers
{
    public class AccountController : Controller
    {
        private readonly OneDbMitraContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly AppSettingService _settingService;

        public AccountController(OneDbMitraContext context, IWebHostEnvironment environment, AppSettingService settingService)
        {
            _context = context;
            _environment = environment;
            _settingService = settingService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            ViewBag.Setting = await _settingService.GetAsync(cancellationToken);
            ViewBag.ActiveMenuCount = await _context.tbl_m_menu.AsNoTracking().CountAsync(m => m.is_aktif, cancellationToken);
            ViewBag.ActiveUserCount = await _context.tbl_m_pengguna.AsNoTracking().CountAsync(u => u.is_aktif, cancellationToken);
            ViewBag.ActiveCompanyCount = await _context.tbl_m_perusahaan.AsNoTracking().CountAsync(c => c.is_aktif, cancellationToken);
            ViewBag.ActiveDepartmentCount = await _context.tbl_m_departemen.AsNoTracking().CountAsync(d => d.is_aktif, cancellationToken);
            var activeWindow = DateTime.UtcNow.AddMinutes(-30);
            ViewBag.ActiveSessionCount = await _context.tbl_r_sesi_aktif.AsNoTracking()
                .CountAsync(s => s.is_aktif && s.last_seen >= activeWindow, cancellationToken);
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewBag.Setting = await _settingService.GetAsync(cancellationToken);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.tbl_m_pengguna
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.username == model.Username && u.kata_sandi == model.Password && u.is_aktif, cancellationToken);

            if (user is null)
            {
                model.ErrorMessage = "Username atau password tidak sesuai.";
                return View(model);
            }

            var roles = await LoadUserRolesAsync(user.pengguna_id, user.peran_id, cancellationToken);
            var primaryRole = roles
                .OrderByDescending(r => r.Level)
                .ThenBy(r => r.RoleId)
                .FirstOrDefault();
            var sessionId = await CreateSessionAsync(user, cancellationToken);
            var claims = await BuildClaimsAsync(user, sessionId, primaryRole?.RoleId, cancellationToken);

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var properties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (!string.IsNullOrWhiteSpace(primaryRole?.StartupUrl) && Url.IsLocalUrl(primaryRole.StartupUrl))
            {
                return Redirect(primaryRole.StartupUrl);
            }

            return RedirectToAction("Index", "MenuAdmin");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await DeactivateSessionAsync();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> OnlineCount(CancellationToken cancellationToken = default)
        {
            var activeWindow = DateTime.UtcNow.AddMinutes(-30);
            var count = await _context.tbl_r_sesi_aktif.AsNoTracking()
                .CountAsync(s => s.is_aktif && s.last_seen >= activeWindow, cancellationToken);
            return Json(new { count });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.tbl_m_pengguna
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.pengguna_id == userId, cancellationToken);

            if (user is null)
            {
                return RedirectToAction("Login");
            }

            var roles = await LoadUserRolesAsync(user.pengguna_id, user.peran_id, cancellationToken);
            var primaryRole = roles
                .OrderByDescending(r => r.Level)
                .ThenBy(r => r.RoleId)
                .FirstOrDefault();

            var company = await _context.tbl_m_perusahaan
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.perusahaan_id == user.perusahaan_id, cancellationToken);

            var department = user.departemen_id.HasValue
                ? await _context.tbl_m_departemen.AsNoTracking().FirstOrDefaultAsync(d => d.departemen_id == user.departemen_id, cancellationToken)
                : null;
            var section = user.seksi_id.HasValue
                ? await _context.tbl_m_seksi.AsNoTracking().FirstOrDefaultAsync(s => s.seksi_id == user.seksi_id, cancellationToken)
                : null;
            var position = user.jabatan_id.HasValue
                ? await _context.tbl_m_jabatan.AsNoTracking().FirstOrDefaultAsync(j => j.jabatan_id == user.jabatan_id, cancellationToken)
                : null;

            var companyHierarchy = await BuildCompanyHierarchyAsync(user.perusahaan_id, cancellationToken);
            var departmentHierarchy = string.IsNullOrWhiteSpace(companyHierarchy)
                ? (department?.nama_departemen ?? "-")
                : string.IsNullOrWhiteSpace(department?.nama_departemen)
                    ? companyHierarchy
                    : $"{companyHierarchy} - {department?.nama_departemen}";

            var model = new ProfileViewModel
            {
                UserId = user.pengguna_id,
                Username = user.username ?? string.Empty,
                FullName = user.nama_lengkap ?? string.Empty,
                PhotoUrl = user.foto_url,
                ThemePreference = user.user_theme,
                FontPrimary = user.user_font_primary,
                FontSecondary = user.user_font_secondary,
                CompanyId = user.perusahaan_id,
                RoleId = user.peran_id,
                DepartmentId = user.departemen_id,
                SectionId = user.seksi_id,
                PositionId = user.jabatan_id,
                CompanyName = company?.nama_perusahaan ?? "-",
                RoleName = primaryRole?.Name ?? "-",
                RoleLevel = primaryRole?.Level ?? 0,
                DepartmentName = department?.nama_departemen ?? "-",
                SectionName = section?.nama_seksi ?? "-",
                PositionName = position?.nama_jabatan ?? "-",
                CompanyHierarchyPath = companyHierarchy,
                DepartmentHierarchyPath = departmentHierarchy
            };

            var currentSessionId = GetSessionId();
            var activity = await _context.tbl_r_audit_log.AsNoTracking()
                .Where(a => a.username != null && a.username == model.Username)
                .OrderByDescending(a => a.audit_id)
                .Take(8)
                .Select(a => new ProfileActivityItem
                {
                    Action = a.aksi ?? "-",
                    Entity = a.entitas ?? "-",
                    Description = a.deskripsi ?? "-",
                    CreatedAt = a.dibuat_pada
                })
                .ToListAsync(cancellationToken);

            var sessions = await _context.tbl_r_sesi_aktif.AsNoTracking()
                .Where(s => s.pengguna_id == userId && s.is_aktif)
                .OrderByDescending(s => s.last_seen ?? s.dibuat_pada)
                .Take(10)
                .Select(s => new ProfileSessionItem
                {
                    SessionId = s.sesi_id,
                    IpAddress = s.ip_address ?? "-",
                    UserAgent = s.user_agent ?? "-",
                    DeviceLabel = BuildDeviceLabel(s.user_agent),
                    LocationLabel = BuildLocationLabel(s.ip_address),
                    CreatedAt = s.dibuat_pada,
                    LastSeen = s.last_seen,
                    IsCurrent = currentSessionId.HasValue && s.sesi_id == currentSessionId.Value
                })
                .ToListAsync(cancellationToken);

            model.Activity = activity;
            model.Sessions = sessions;

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = GetUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.tbl_m_pengguna
                .FirstOrDefaultAsync(u => u.pengguna_id == userId, cancellationToken);

            if (user is null)
            {
                return RedirectToAction("Login");
            }

            if (!string.IsNullOrWhiteSpace(model.PhotoBase64))
            {
                var photoPath = SaveProfilePhoto(userId, model.PhotoBase64);
                user.foto_url = photoPath;
            }
            else
            {
                user.foto_url = string.IsNullOrWhiteSpace(model.PhotoUrl) ? null : model.PhotoUrl.Trim();
            }
            user.diubah_pada = DateTime.UtcNow;

            var wantsPasswordChange = !string.IsNullOrWhiteSpace(model.NewPassword);
            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword) || model.CurrentPassword != user.kata_sandi)
                {
                    ModelState.AddModelError(nameof(ProfileViewModel.CurrentPassword), "Password saat ini tidak sesuai.");
                    return View(model);
                }

                user.kata_sandi = model.NewPassword!;
            }

            user.user_theme = string.IsNullOrWhiteSpace(model.ThemePreference) ? null : model.ThemePreference.Trim();
            user.user_font_primary = string.IsNullOrWhiteSpace(model.FontPrimary) ? null : model.FontPrimary.Trim();
            user.user_font_secondary = string.IsNullOrWhiteSpace(model.FontSecondary) ? null : model.FontSecondary.Trim();

            await _context.SaveChangesAsync(cancellationToken);

            await RefreshSignInAsync(user, cancellationToken);

            TempData["ProfileMessage"] = "Profil berhasil diperbarui.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSession(int sessionId, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login");
            }

            var session = await _context.tbl_r_sesi_aktif
                .FirstOrDefaultAsync(s => s.sesi_id == sessionId, cancellationToken);

            if (session is null)
            {
                TempData["AlertMessage"] = "Sesi tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Profile));
            }

            var isOwner = string.Equals(User.FindFirstValue(ClaimTypes.Role), "Owner", System.StringComparison.OrdinalIgnoreCase);
            if (session.pengguna_id != userId && !isOwner)
            {
                TempData["AlertMessage"] = "Tidak memiliki izin untuk revoke sesi ini.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Profile));
            }

            session.is_aktif = false;
            session.revoked_at = DateTime.UtcNow;
            session.revoked_by = User.Identity?.Name ?? "system";
            await _context.SaveChangesAsync(cancellationToken);

            if (GetSessionId() == sessionId)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            TempData["AlertMessage"] = "Sesi berhasil direvoke.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login");
            }

            var currentSessionId = GetSessionId();
            var sessions = await _context.tbl_r_sesi_aktif
                .Where(s => s.pengguna_id == userId && s.is_aktif && (!currentSessionId.HasValue || s.sesi_id != currentSessionId.Value))
                .ToListAsync(cancellationToken);

            if (sessions.Count == 0)
            {
                TempData["AlertMessage"] = "Tidak ada sesi lain yang bisa direvoke.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var session in sessions)
            {
                session.is_aktif = false;
                session.revoked_at = DateTime.UtcNow;
                session.revoked_by = User.Identity?.Name ?? "system";
            }

            await _context.SaveChangesAsync(cancellationToken);
            TempData["AlertMessage"] = "Semua sesi lain berhasil direvoke.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwitchRole(int roleId, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login");
            }

            var allowed = await _context.tbl_r_pengguna_peran.AsNoTracking()
                .AnyAsync(rel => rel.pengguna_id == userId && rel.peran_id == roleId && rel.is_aktif, cancellationToken);
            if (!allowed)
            {
                var fallback = await _context.tbl_m_pengguna.AsNoTracking()
                    .AnyAsync(u => u.pengguna_id == userId && u.peran_id == roleId, cancellationToken);
                if (!fallback)
                {
                    TempData["AlertMessage"] = "Role tidak tersedia untuk akun ini.";
                    TempData["AlertType"] = "warning";
                    return RedirectToAction("Index", "MenuAdmin");
                }
            }

            var user = await _context.tbl_m_pengguna.AsNoTracking()
                .FirstOrDefaultAsync(u => u.pengguna_id == userId, cancellationToken);
            if (user is null)
            {
                return RedirectToAction("Login");
            }

            var sessionId = GetSessionId();
            var claims = await BuildClaimsAsync(user, sessionId, roleId, cancellationToken);
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = existing?.Properties ?? new AuthenticationProperties();
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "MenuAdmin");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Impersonate(int userId, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var currentUserId = GetUserId();
            if (currentUserId <= 0)
            {
                return RedirectToAction("Login");
            }

            if (!await IsOwnerAsync(currentUserId, cancellationToken))
            {
                TempData["AlertMessage"] = "Tidak memiliki izin untuk impersonate.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("Index", "MenuAdmin");
            }

            var user = await _context.tbl_m_pengguna.AsNoTracking()
                .FirstOrDefaultAsync(u => u.pengguna_id == userId && u.is_aktif, cancellationToken);
            if (user is null)
            {
                TempData["AlertMessage"] = "User tidak ditemukan.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("Index", "UserAdmin");
            }

            var sessionId = await CreateSessionAsync(user, cancellationToken);
            var claims = await BuildClaimsAsync(user, sessionId, null, cancellationToken);
            claims.Add(new Claim("impersonator_id", currentUserId.ToString()));
            claims.Add(new Claim("impersonator_name", User.Identity?.Name ?? "Owner"));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = existing?.Properties ?? new AuthenticationProperties();
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "MenuAdmin");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StopImpersonate(string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var impersonatorIdValue = User.FindFirstValue("impersonator_id");
            if (!int.TryParse(impersonatorIdValue, out var impersonatorId) || impersonatorId <= 0)
            {
                return RedirectToAction("Index", "MenuAdmin");
            }

            var user = await _context.tbl_m_pengguna.AsNoTracking()
                .FirstOrDefaultAsync(u => u.pengguna_id == impersonatorId && u.is_aktif, cancellationToken);
            if (user is null)
            {
                return RedirectToAction("Login");
            }

            var sessionId = await CreateSessionAsync(user, cancellationToken);
            var claims = await BuildClaimsAsync(user, sessionId, null, cancellationToken);
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = existing?.Properties ?? new AuthenticationProperties();
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "MenuAdmin");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login");
            }

            var sessions = await _context.tbl_r_sesi_aktif
                .Where(s => s.pengguna_id == userId && s.is_aktif)
                .ToListAsync(cancellationToken);

            if (sessions.Count == 0)
            {
                TempData["AlertMessage"] = "Tidak ada sesi aktif.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var session in sessions)
            {
                session.is_aktif = false;
                session.revoked_at = DateTime.UtcNow;
                session.revoked_by = User.Identity?.Name ?? "system";
            }

            await _context.SaveChangesAsync(cancellationToken);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private int GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }

        private async Task<List<Claim>> BuildClaimsAsync(one_db_mitra.Models.Db.tbl_m_pengguna user, int? sessionId, int? primaryRoleId, CancellationToken cancellationToken)
        {
            var roles = await LoadUserRolesAsync(user.pengguna_id, user.peran_id, cancellationToken);
            var primaryRole = roles
                .OrderByDescending(r => r.Level)
                .ThenBy(r => r.RoleId)
                .FirstOrDefault();
            if (primaryRoleId.HasValue)
            {
                var selected = roles.FirstOrDefault(r => r.RoleId == primaryRoleId.Value);
                if (selected is not null)
                {
                    primaryRole = selected;
                }
            }

            var company = await _context.tbl_m_perusahaan
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.perusahaan_id == user.perusahaan_id, cancellationToken);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.pengguna_id.ToString()),
                new(ClaimTypes.Name, user.username ?? string.Empty),
                new("role_id", (primaryRole?.RoleId ?? user.peran_id).ToString()),
                new("company_id", user.perusahaan_id.ToString()),
                new("department_id", user.departemen_id?.ToString() ?? string.Empty),
                new("section_id", user.seksi_id?.ToString() ?? string.Empty),
                new("position_id", user.jabatan_id?.ToString() ?? string.Empty)
            };

            if (primaryRole is not null && !string.IsNullOrWhiteSpace(primaryRole.Name))
            {
                claims.Add(new Claim(ClaimTypes.Role, primaryRole.Name));
            }

            foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r.Name)))
            {
                if (primaryRole is not null && string.Equals(role.Name, primaryRole.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                claims.Add(new Claim(ClaimTypes.Role, role.Name));
            }

            if (roles.Count > 0)
            {
                var roleIds = string.Join(",", roles.Select(r => r.RoleId));
                claims.Add(new Claim("role_ids", roleIds));
            }

            if (sessionId.HasValue && sessionId.Value > 0)
            {
                claims.Add(new Claim("session_id", sessionId.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(user.nama_lengkap))
            {
                claims.Add(new Claim("full_name", user.nama_lengkap));
            }

            if (!string.IsNullOrWhiteSpace(company?.nama_perusahaan))
            {
                claims.Add(new Claim("company_name", company.nama_perusahaan));
            }

            if (!string.IsNullOrWhiteSpace(user.foto_url))
            {
                claims.Add(new Claim("profile_photo", user.foto_url));
            }

            if (!string.IsNullOrWhiteSpace(user.user_theme))
            {
                claims.Add(new Claim("user_theme", user.user_theme));
            }

            if (!string.IsNullOrWhiteSpace(user.user_font_primary))
            {
                claims.Add(new Claim("user_font_primary", user.user_font_primary));
            }

            if (!string.IsNullOrWhiteSpace(user.user_font_secondary))
            {
                claims.Add(new Claim("user_font_secondary", user.user_font_secondary));
            }

            return claims;
        }

        private async Task RefreshSignInAsync(one_db_mitra.Models.Db.tbl_m_pengguna user, CancellationToken cancellationToken)
        {
            var sessionId = GetSessionId();
            var primaryRoleId = GetPrimaryRoleId();
            var claims = await BuildClaimsAsync(user, sessionId, primaryRoleId, cancellationToken);
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = existing?.Properties ?? new AuthenticationProperties();
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        }

        private int? GetSessionId()
        {
            var value = User.FindFirstValue("session_id");
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private int? GetPrimaryRoleId()
        {
            var value = User.FindFirstValue("role_id");
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private async Task<bool> IsOwnerAsync(int userId, CancellationToken cancellationToken)
        {
            var roleIdValue = User.FindFirstValue("role_id");
            if (!int.TryParse(roleIdValue, out var roleId) || roleId <= 0)
            {
                var fallbackRoleId = await _context.tbl_m_pengguna.AsNoTracking()
                    .Where(u => u.pengguna_id == userId)
                    .Select(u => u.peran_id)
                    .FirstOrDefaultAsync(cancellationToken);
                roleId = fallbackRoleId;
            }

            if (roleId <= 0)
            {
                return false;
            }

            var level = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.peran_id == roleId)
                .Select(r => r.level_akses)
                .FirstOrDefaultAsync(cancellationToken);

            return level >= 4;
        }

        private async Task<int?> CreateSessionAsync(one_db_mitra.Models.Db.tbl_m_pengguna user, CancellationToken cancellationToken)
        {
            var session = new one_db_mitra.Models.Db.tbl_r_sesi_aktif
            {
                pengguna_id = user.pengguna_id,
                username = user.username,
                ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                user_agent = Request.Headers.UserAgent.ToString(),
                dibuat_pada = DateTime.UtcNow,
                last_seen = DateTime.UtcNow,
                is_aktif = true
            };

            _context.tbl_r_sesi_aktif.Add(session);
            await _context.SaveChangesAsync(cancellationToken);
            return session.sesi_id;
        }

        private async Task DeactivateSessionAsync()
        {
            var sessionId = GetSessionId();
            if (!sessionId.HasValue)
            {
                return;
            }

            var session = await _context.tbl_r_sesi_aktif
                .FirstOrDefaultAsync(s => s.sesi_id == sessionId.Value);
            if (session is null)
            {
                return;
            }

            session.is_aktif = false;
            session.revoked_at = DateTime.UtcNow;
            session.revoked_by = User.Identity?.Name ?? "system";
            await _context.SaveChangesAsync();
        }

        private string SaveProfilePhoto(int userId, string base64Image)
        {
            var commaIndex = base64Image.IndexOf(',');
            var cleanBase64 = commaIndex >= 0 ? base64Image[(commaIndex + 1)..] : base64Image;

            var bytes = Convert.FromBase64String(cleanBase64);
            var folderPath = Path.Combine(_environment.WebRootPath, "uploads", "profile");
            Directory.CreateDirectory(folderPath);

            var fileName = $"user_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var filePath = Path.Combine(folderPath, fileName);
            System.IO.File.WriteAllBytes(filePath, bytes);

            return $"/uploads/profile/{fileName}";
        }

        private async Task<List<RoleInfo>> LoadUserRolesAsync(int userId, int primaryRoleId, CancellationToken cancellationToken)
        {
            var roles = await (from rel in _context.tbl_r_pengguna_peran.AsNoTracking()
                               join role in _context.tbl_m_peran.AsNoTracking() on rel.peran_id equals role.peran_id
                               where rel.pengguna_id == userId && rel.is_aktif && role.is_aktif
                               select new RoleInfo
                               {
                                   RoleId = role.peran_id,
                                   Name = role.nama_peran ?? string.Empty,
                                   Level = role.level_akses,
                                   StartupUrl = role.startup_url
                               })
                .ToListAsync(cancellationToken);

            if (roles.Count == 0 && primaryRoleId > 0)
            {
                var fallback = await _context.tbl_m_peran.AsNoTracking()
                    .Where(r => r.peran_id == primaryRoleId && r.is_aktif)
                    .Select(r => new RoleInfo
                    {
                        RoleId = r.peran_id,
                        Name = r.nama_peran ?? string.Empty,
                        Level = r.level_akses,
                        StartupUrl = r.startup_url
                    })
                    .ToListAsync(cancellationToken);
                roles.AddRange(fallback);
            }

            return roles;
        }

        private class RoleInfo
        {
            public int RoleId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Level { get; set; }
            public string? StartupUrl { get; set; }
        }

        private async Task<string> BuildCompanyHierarchyAsync(int companyId, CancellationToken cancellationToken)
        {
            if (companyId <= 0)
            {
                return string.Empty;
            }

            var companies = await _context.tbl_m_perusahaan.AsNoTracking()
                .ToListAsync(cancellationToken);
            var map = companies.ToDictionary(c => c.perusahaan_id);

            var path = new List<string>();
            var visited = new HashSet<int>();
            var currentId = companyId;

            while (currentId > 0 && map.TryGetValue(currentId, out var company) && visited.Add(currentId))
            {
                path.Add(company.nama_perusahaan ?? string.Empty);
                currentId = company.perusahaan_induk_id ?? 0;
            }

            path.Reverse();
            return string.Join(" - ", path.Where(name => !string.IsNullOrWhiteSpace(name)));
        }

        private static string BuildDeviceLabel(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "-";
            }

            var agent = userAgent.ToLowerInvariant();
            if (agent.Contains("iphone") || agent.Contains("ios"))
            {
                return "iPhone";
            }

            if (agent.Contains("ipad"))
            {
                return "iPad";
            }

            if (agent.Contains("android"))
            {
                return "Android";
            }

            if (agent.Contains("windows"))
            {
                return "Windows";
            }

            if (agent.Contains("mac"))
            {
                return "Mac";
            }

            if (agent.Contains("linux"))
            {
                return "Linux";
            }

            return "Unknown Device";
        }

        private static string BuildLocationLabel(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return "-";
            }

            if (ipAddress.StartsWith("10.", StringComparison.Ordinal)
                || ipAddress.StartsWith("192.168.", StringComparison.Ordinal))
            {
                return "Internal Network";
            }

            if (ipAddress.StartsWith("172.", StringComparison.Ordinal))
            {
                var parts = ipAddress.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && int.TryParse(parts[1], out var second) && second >= 16 && second <= 31)
                {
                    return "Internal Network";
                }
            }

            return "Public Network";
        }
    }
}
