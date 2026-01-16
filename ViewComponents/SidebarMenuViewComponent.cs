using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using one_db_mitra.Models.Menu;
using one_db_mitra.Services.Menu;

namespace one_db_mitra.ViewComponents
{
    public class SidebarMenuViewComponent : ViewComponent
    {
        private readonly MenuProfileService _menuProfileService;

        public SidebarMenuViewComponent(MenuProfileService menuProfileService)
        {
            _menuProfileService = menuProfileService;
        }

        public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken = default)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View(Array.Empty<MenuItem>());
            }

            var scope = new MenuScope
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
            return View(tree);
        }

        private string BuildSessionKey()
        {
            var principal = User as ClaimsPrincipal;
            var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
            var companyId = GetClaimInt("company_id").ToString();
            var roleIds = string.Join("-", GetRoleIds());
            var departmentId = GetOptionalClaimInt("department_id")?.ToString() ?? "0";
            var sectionId = GetOptionalClaimInt("section_id")?.ToString() ?? "0";
            var positionId = GetOptionalClaimInt("position_id")?.ToString() ?? "0";

            return $"{userId}:{companyId}:{roleIds}:{departmentId}:{sectionId}:{positionId}".ToLowerInvariant();
        }

        private int GetClaimInt(string claimType)
        {
            var value = (User as ClaimsPrincipal)?.FindFirst(claimType)?.Value;
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }

        private int? GetOptionalClaimInt(string claimType)
        {
            var value = (User as ClaimsPrincipal)?.FindFirst(claimType)?.Value;
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private IReadOnlyList<int> GetRoleIds()
        {
            var value = (User as ClaimsPrincipal)?.FindFirst("role_ids")?.Value;
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
