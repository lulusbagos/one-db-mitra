using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using one_db_mitra.Data;
using one_db_mitra.Models.Db;
using one_db_mitra.Models.Menu;

namespace one_db_mitra.Services.Menu
{
    public class DbMenuRepository : IMenuRepository
    {
        private readonly OneDbMitraContext _context;

        public DbMenuRepository(OneDbMitraContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<MenuItem>> GetMenuTreeAsync(MenuScope scope, CancellationToken cancellationToken = default)
        {
            if (scope is null || scope.CompanyId <= 0)
            {
                return Array.Empty<MenuItem>();
            }

            var roleIds = scope.RoleIds is { Count: > 0 }
                ? scope.RoleIds
                : (scope.RoleId > 0 ? new[] { scope.RoleId } : Array.Empty<int>());

            if (roleIds.Count == 0)
            {
                return Array.Empty<MenuItem>();
            }

            var roleMenuIds = await _context.tbl_r_menu_peran
                .AsNoTracking()
                .Where(rel => rel.is_aktif && roleIds.Contains(rel.peran_id))
                .Select(rel => rel.menu_id)
                .ToListAsync(cancellationToken);

            if (roleMenuIds.Count == 0)
            {
                return Array.Empty<MenuItem>();
            }

            var allMenus = await _context.tbl_m_menu
                .AsNoTracking()
                .Where(menu => menu.is_aktif)
                .ToListAsync(cancellationToken);

            var roleLookup = await (from rel in _context.tbl_r_menu_peran.AsNoTracking()
                                    join role in _context.tbl_m_peran.AsNoTracking() on rel.peran_id equals role.peran_id
                                    where rel.is_aktif && role.is_aktif && roleIds.Contains(role.peran_id)
                                    select new { rel.menu_id, role.nama_peran })
                .ToListAsync(cancellationToken);

            var menuCompanyLookup = await _context.tbl_r_menu_perusahaan
                .AsNoTracking()
                .Where(rel => rel.is_aktif && roleMenuIds.Contains(rel.menu_id))
                .ToListAsync(cancellationToken);

            var companyMap = menuCompanyLookup
                .GroupBy(item => item.menu_id)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.perusahaan_id).Distinct().ToHashSet());

            var rolesByMenuId = roleLookup
                .GroupBy(item => item.menu_id)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.nama_peran)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList());

            var filteredMenus = allMenus
                .Where(menu => roleMenuIds.Contains(menu.menu_id))
                .Where(menu =>
                {
                    if (companyMap.TryGetValue(menu.menu_id, out var companies))
                    {
                        return companies.Contains(scope.CompanyId);
                    }

                    return true;
                })
                .ToList();

            var menuById = allMenus.ToDictionary(menu => menu.menu_id);
            var allowed = new HashSet<int>(filteredMenus.Select(menu => menu.menu_id));

            foreach (var menu in filteredMenus)
            {
                var parentId = menu.menu_induk_id;
                while (parentId.HasValue && menuById.TryGetValue(parentId.Value, out var parent))
                {
                    if (!allowed.Add(parent.menu_id))
                    {
                        break;
                    }

                    parentId = parent.menu_induk_id;
                }
            }

            var items = allMenus
                .Where(menu => allowed.Contains(menu.menu_id))
                .Select(menu => MapToMenuItem(menu, rolesByMenuId.TryGetValue(menu.menu_id, out var roles) ? roles : null))
                .ToList();

            return BuildMenuTree(items);
        }

        public async Task<MenuOperationResult> AddMenuAsync(MenuEditRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return MenuOperationResult.Fail("Request tidak boleh kosong.");
            }

            var code = NormalizeMenuCode(request.MenuCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                return MenuOperationResult.Fail("Kode menu wajib diisi.");
            }

            if (await MenuCodeExistsAsync(code, cancellationToken))
            {
                return MenuOperationResult.Fail("Kode menu sudah digunakan.");
            }

            var entity = BuildMenuEntity(request, parentId: null, code);
            _context.tbl_m_menu.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return MenuOperationResult.Ok(MapToMenuItem(entity));
        }

        public async Task<MenuOperationResult> AddSubMenuAsync(int parentMenuId, MenuEditRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return MenuOperationResult.Fail("Request tidak boleh kosong.");
            }

            var parentExists = await _context.tbl_m_menu
                .AsNoTracking()
                .AnyAsync(menu => menu.menu_id == parentMenuId, cancellationToken);

            if (!parentExists)
            {
                return MenuOperationResult.Fail("Menu induk tidak ditemukan.");
            }

            var code = NormalizeMenuCode(request.MenuCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                return MenuOperationResult.Fail("Kode menu wajib diisi.");
            }

            if (await MenuCodeExistsAsync(code, cancellationToken))
            {
                return MenuOperationResult.Fail("Kode menu sudah digunakan.");
            }

            var entity = BuildMenuEntity(request, parentMenuId, code);
            _context.tbl_m_menu.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return MenuOperationResult.Ok(MapToMenuItem(entity));
        }

        public async Task<MenuOperationResult> UpdateMenuFlagsAsync(int menuId, bool isHidden, bool openInNewTab, CancellationToken cancellationToken = default)
        {
            var entity = await _context.tbl_m_menu
                .FirstOrDefaultAsync(menu => menu.menu_id == menuId, cancellationToken);

            if (entity is null)
            {
                return MenuOperationResult.Fail("Menu tidak ditemukan.");
            }

            entity.sembunyikan = isHidden;
            entity.tab_baru = openInNewTab;
            entity.diubah_pada = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return MenuOperationResult.Ok(MapToMenuItem(entity));
        }

        private static MenuItem MapToMenuItem(tbl_m_menu menu, IEnumerable<string>? roles = null)
        {
            var item = new MenuItem
            {
                Id = menu.menu_id,
                ParentId = menu.menu_induk_id,
                MenuCode = menu.kode_menu ?? string.Empty,
                DisplayName = menu.nama_tampil ?? string.Empty,
                IconKey = string.IsNullOrWhiteSpace(menu.ikon) ? "bi-circle" : menu.ikon,
                UrlPath = string.IsNullOrWhiteSpace(menu.url) ? "#" : menu.url,
                IsHidden = menu.sembunyikan,
                OpenInNewTab = menu.tab_baru,
                SortOrder = menu.urutan,
                StartupCategory = "general"
            };

            if (roles is not null)
            {
                item.AllowedRoles = roles.ToList();
            }

            return item;
        }

        private static IReadOnlyList<MenuItem> BuildMenuTree(List<MenuItem> items)
        {
            var byId = items.ToDictionary(item => item.Id);
            var roots = new List<MenuItem>();

            foreach (var item in items)
            {
                if (item.ParentId.HasValue && byId.TryGetValue(item.ParentId.Value, out var parent))
                {
                    parent.Children.Add(item);
                }
                else
                {
                    roots.Add(item);
                }
            }

            SortTree(roots);
            return roots;
        }

        private static void SortTree(List<MenuItem> nodes)
        {
            nodes.Sort((a, b) =>
            {
                var result = a.SortOrder.CompareTo(b.SortOrder);
                return result != 0
                    ? result
                    : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var node in nodes)
            {
                if (node.Children.Count > 0)
                {
                    SortTree(node.Children);
                }
            }
        }

        private static string NormalizeMenuCode(string code)
        {
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
        }

        private async Task<bool> MenuCodeExistsAsync(string code, CancellationToken cancellationToken)
        {
            return await _context.tbl_m_menu
                .AsNoTracking()
                .AnyAsync(menu => menu.kode_menu != null && menu.kode_menu.ToUpper() == code, cancellationToken);
        }

        private static tbl_m_menu BuildMenuEntity(MenuEditRequest request, int? parentId, string code)
        {
            return new tbl_m_menu
            {
                menu_induk_id = parentId,
                kode_menu = code,
                nama_tampil = request.DisplayName.Trim(),
                ikon = string.IsNullOrWhiteSpace(request.IconKey) ? "bi-circle" : request.IconKey.Trim(),
                url = string.IsNullOrWhiteSpace(request.UrlPath) ? "#" : request.UrlPath.Trim(),
                urutan = request.SortOrder,
                sembunyikan = request.IsHidden,
                tab_baru = request.OpenInNewTab,
                is_aktif = true,
                diubah_pada = null
            };
        }
    }
}
