using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using one_db_mitra.Models.Menu;

namespace one_db_mitra.Services.Menu
{
    /// <summary>
    /// Repository sementara agar UI bisa di-demokan tanpa database. Struktur dibuat menyerupai konfigurasi menu produksi.
    /// </summary>
    public class InMemoryMenuRepository : IMenuRepository
    {
        private static readonly object _lock = new();
        private static int _nextId = 1000;

        private static readonly List<MenuItem> MenuStore = new()
        {
            new MenuItem
            {
                Id = 1,
                MenuCode = "DASH",
                DisplayName = "Dasbor Perusahaan",
                IconKey = "bi-speedometer2",
                UrlPath = "/Dashboard",
                SortOrder = 1,
                AllowedCategories = new() { "SuperAdmin", "Owner" },
                AllowedRoles = new() { "owner", "main", "sub", "vendor" },
                Children =
                {
                    new MenuItem
                    {
                        Id = 11,
                        ParentId = 1,
                        MenuCode = "DASH_OWNER",
                        DisplayName = "Ringkasan Owner",
                        IconKey = "bi-diagram-3",
                        UrlPath = "/Dashboard/Owner",
                        SortOrder = 1
                    },
                    new MenuItem
                    {
                        Id = 12,
                        ParentId = 1,
                        MenuCode = "DASH_VENDOR",
                        DisplayName = "Vendor Insight",
                        IconKey = "bi-graph-up",
                        UrlPath = "/Dashboard/Vendor",
                        SortOrder = 2
                    }
                }
            },
            new MenuItem
            {
                Id = 2,
                MenuCode = "MASTER",
                DisplayName = "Master Data",
                IconKey = "bi-collection",
                UrlPath = "/Master",
                SortOrder = 2,
                AllowedCategories = new() { "SuperAdmin", "MainContractor" },
                AllowedRoles = new() { "owner", "main" },
                Children =
                {
                    new MenuItem
                    {
                        Id = 21,
                        ParentId = 2,
                        MenuCode = "MASTER_MENU",
                        DisplayName = "Pengaturan Menu",
                        IconKey = "bi-list-check",
                        UrlPath = "/Dashboard#menu",
                        SortOrder = 1
                    },
                    new MenuItem
                    {
                        Id = 22,
                        ParentId = 2,
                        MenuCode = "MASTER_ROLE",
                        DisplayName = "Role & Kategori",
                        IconKey = "bi-shield-lock",
                        UrlPath = "/Dashboard#role",
                        SortOrder = 2
                    }
                }
            },
            new MenuItem
            {
                Id = 3,
                MenuCode = "SETTING",
                DisplayName = "Preferensi",
                IconKey = "bi-gear",
                UrlPath = "/Settings",
                SortOrder = 3,
                AllowedCategories = new() { "SuperAdmin", "Owner", "Vendor" },
                AllowedRoles = new() { "owner", "main", "sub", "vendor" }
            }
        };

        public Task<IReadOnlyList<MenuItem>> GetMenuTreeAsync(MenuScope scope, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var clone = MenuStore
                    .OrderBy(menu => menu.SortOrder)
                    .Select(menu => menu.Clone())
                    .ToList();

                return Task.FromResult<IReadOnlyList<MenuItem>>(clone);
            }
        }

        public Task<MenuOperationResult> AddMenuAsync(MenuEditRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Task.FromResult(MenuOperationResult.Fail("Request tidak boleh kosong."));
            }

            lock (_lock)
            {
                if (MenuCodeExists(MenuStore, request.MenuCode))
                {
                    return Task.FromResult(MenuOperationResult.Fail("Kode menu sudah digunakan."));
                }

                var newMenu = BuildMenuEntity(request, parentId: null);
                MenuStore.Add(newMenu);
                MenuStore.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

                return Task.FromResult(MenuOperationResult.Ok(newMenu.Clone()));
            }
        }

        public Task<MenuOperationResult> AddSubMenuAsync(int parentMenuId, MenuEditRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Task.FromResult(MenuOperationResult.Fail("Request tidak boleh kosong."));
            }

            lock (_lock)
            {
                var parentMenu = FindMenu(MenuStore, parentMenuId);
                if (parentMenu is null)
                {
                    return Task.FromResult(MenuOperationResult.Fail("Menu induk tidak ditemukan."));
                }

                if (MenuCodeExists(MenuStore, request.MenuCode))
                {
                    return Task.FromResult(MenuOperationResult.Fail("Kode menu sudah digunakan."));
                }

                var newMenu = BuildMenuEntity(request, parentMenu.Id);
                parentMenu.Children.Add(newMenu);
                parentMenu.Children = parentMenu.Children.OrderBy(menu => menu.SortOrder).ToList();

                return Task.FromResult(MenuOperationResult.Ok(newMenu.Clone()));
            }
        }

        public Task<MenuOperationResult> UpdateMenuFlagsAsync(int menuId, bool isHidden, bool openInNewTab, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var menu = FindMenu(MenuStore, menuId);
                if (menu is null)
                {
                    return Task.FromResult(MenuOperationResult.Fail("Menu tidak ditemukan."));
                }

                menu.IsHidden = isHidden;
                menu.OpenInNewTab = openInNewTab;

                return Task.FromResult(MenuOperationResult.Ok(menu.Clone()));
            }
        }

        private static MenuItem? FindMenu(IEnumerable<MenuItem> source, int id)
        {
            foreach (var menu in source)
            {
                if (menu.Id == id)
                {
                    return menu;
                }

                var child = FindMenu(menu.Children, id);
                if (child is not null)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool MenuCodeExists(IEnumerable<MenuItem> source, string code)
        {
            return source.Any(menu => menu.MenuCode.Equals(code, StringComparison.OrdinalIgnoreCase) || MenuCodeExists(menu.Children, code));
        }

        private static MenuItem BuildMenuEntity(MenuEditRequest request, int? parentId)
        {
            return new MenuItem
            {
                Id = Interlocked.Increment(ref _nextId),
                ParentId = parentId,
                MenuCode = request.MenuCode.Trim().ToUpperInvariant(),
                DisplayName = request.DisplayName.Trim(),
                IconKey = string.IsNullOrWhiteSpace(request.IconKey) ? "bi-circle" : request.IconKey.Trim(),
                UrlPath = string.IsNullOrWhiteSpace(request.UrlPath) ? "#" : request.UrlPath.Trim(),
                OpenInNewTab = request.OpenInNewTab,
                IsHidden = request.IsHidden,
                SortOrder = request.SortOrder,
                StartupCategory = string.IsNullOrWhiteSpace(request.StartupCategory) ? "default" : request.StartupCategory.Trim(),
                AllowedCategories = new() { "SuperAdmin", "Owner", "MainContractor", "SubContractor", "Vendor" },
                AllowedRoles = new() { "owner", "main", "sub", "vendor" },
                Children = new List<MenuItem>()
            };
        }
    }
}
