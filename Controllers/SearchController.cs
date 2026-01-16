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
using one_db_mitra.Services.Menu;

namespace one_db_mitra.Controllers
{
    [Authorize]
    public class SearchController : Controller
    {
        private readonly OneDbMitraContext _context;
        private readonly MenuProfileService _menuProfileService;

        public SearchController(OneDbMitraContext context, MenuProfileService menuProfileService)
        {
            _context = context;
            _menuProfileService = menuProfileService;
        }

        [HttpGet]
        public async Task<IActionResult> Quick(string? q, CancellationToken cancellationToken = default)
        {
            var query = q?.Trim();
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new { items = Array.Empty<object>() });
            }

            var items = new List<object>();
            var companyId = GetClaimInt("company_id");

            var menus = await LoadMenuMatchesAsync(query, cancellationToken);
            items.AddRange(menus);

            var roles = await _context.tbl_m_peran.AsNoTracking()
                .Where(r => r.is_aktif && r.nama_peran.Contains(query))
                .OrderBy(r => r.nama_peran)
                .Take(6)
                .Select(r => new
                {
                    label = r.nama_peran,
                    category = "Role",
                    url = Url.Action("Index", "UserAdmin")
                })
                .ToListAsync(cancellationToken);
            items.AddRange(roles);

            if (companyId > 0)
            {
                var users = await _context.tbl_m_pengguna.AsNoTracking()
                    .Where(u => u.is_aktif && u.perusahaan_id == companyId
                        && ((u.username != null && u.username.Contains(query)) || (u.nama_lengkap != null && u.nama_lengkap.Contains(query))))
                    .OrderBy(u => u.username)
                    .Take(6)
                    .Select(u => new
                    {
                        label = (u.nama_lengkap ?? u.username ?? "User"),
                        category = "User",
                        url = Url.Action("Edit", "UserAdmin", new { id = u.pengguna_id })
                    })
                    .ToListAsync(cancellationToken);
                items.AddRange(users);
            }

            return Json(new { items = items.Take(12) });
        }

        private async Task<IEnumerable<object>> LoadMenuMatchesAsync(string query, CancellationToken cancellationToken)
        {
            var scope = new Models.Menu.MenuScope
            {
                CompanyId = GetClaimInt("company_id"),
                RoleId = GetClaimInt("role_id"),
                RoleIds = GetRoleIds(),
                DepartmentId = GetOptionalClaimInt("department_id"),
                SectionId = GetOptionalClaimInt("section_id"),
                PositionId = GetOptionalClaimInt("position_id")
            };

            var sessionKey = BuildSessionKey();
            var tree = await _menuProfileService.GetMenusForSessionAsync(sessionKey, scope, cancellationToken);
            var flat = FlattenMenus(tree);

            return flat
                .Where(menu => !string.IsNullOrWhiteSpace(menu.UrlPath) && menu.UrlPath != "#")
                .Where(menu => menu.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || menu.MenuCode.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .Select(menu => new
                {
                    label = $"{menu.DisplayName} ({menu.MenuCode})",
                    category = "Menu",
                    url = menu.UrlPath
                })
                .ToList();
        }

        private static IEnumerable<Models.Menu.MenuItem> FlattenMenus(IEnumerable<Models.Menu.MenuItem> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;
                if (node.Children.Count > 0)
                {
                    foreach (var child in FlattenMenus(node.Children))
                    {
                        yield return child;
                    }
                }
            }
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
