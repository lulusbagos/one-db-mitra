using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using one_db_mitra.Models.Menu;
using one_db_mitra.Services.Menu;

namespace one_db_mitra.Pages.Dashboard
{
    public class IndexModel : PageModel
    {
        private readonly MenuProfileService _menuProfileService;
        private readonly IMenuRepository _menuRepository;

        public IndexModel(MenuProfileService menuProfileService, IMenuRepository menuRepository)
        {
            _menuProfileService = menuProfileService;
            _menuRepository = menuRepository;
        }

        public IReadOnlyList<MenuItem> MenuTree { get; private set; } = Array.Empty<MenuItem>();
        public IEnumerable<SelectListItem> ParentMenuOptions { get; private set; } = Enumerable.Empty<SelectListItem>();

        [BindProperty]
        public MenuFormModel NewMenu { get; set; } = new();

        [BindProperty]
        public SubMenuFormModel NewSubMenu { get; set; } = new();

        [TempData]
        public string? Notification { get; set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await LoadDataAsync(cancellationToken);
        }

        public async Task<IActionResult> OnPostAddMenuAsync(CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await LoadDataAsync(cancellationToken);
                return Page();
            }

            var result = await _menuRepository.AddMenuAsync(NewMenu.ToRequest(), cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "Gagal menambahkan menu.");
                await LoadDataAsync(cancellationToken);
                return Page();
            }

            Notification = $"Menu '{result.Menu?.DisplayName}' berhasil ditambahkan.";
            _menuProfileService.InvalidateSessionMenu("demo");
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddSubMenuAsync(CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await LoadDataAsync(cancellationToken);
                return Page();
            }

            var request = NewSubMenu.ToRequest();
            var result = await _menuRepository.AddSubMenuAsync(NewSubMenu.ParentMenuId, request, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "Gagal menambahkan sub menu.");
                await LoadDataAsync(cancellationToken);
                return Page();
            }

            Notification = $"Sub menu '{result.Menu?.DisplayName}' berhasil ditambahkan.";
            _menuProfileService.InvalidateSessionMenu("demo");
            return RedirectToPage();
        }

        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            MenuTree = await _menuProfileService.GetMenusForSessionAsync("demo", new MenuScope { CompanyId = 1, RoleId = 1 }, cancellationToken);
            ParentMenuOptions = BuildOptions(MenuTree);
        }

        private static IEnumerable<SelectListItem> BuildOptions(IEnumerable<MenuItem> menus, string prefix = "")
        {
            foreach (var menu in menus.OrderBy(m => m.SortOrder))
            {
                var label = string.IsNullOrWhiteSpace(prefix) ? menu.DisplayName : $"{prefix} / {menu.DisplayName}";
                yield return new SelectListItem(label, menu.Id.ToString());

                if (menu.Children.Any())
                {
                    foreach (var child in BuildOptions(menu.Children, label))
                    {
                        yield return child;
                    }
                }
            }
        }

        public class MenuFormModel
        {
            [Required(ErrorMessage = "Nama menu wajib diisi.")]
            [Display(Name = "Nama Menu")]
            [StringLength(80)]
            public string DisplayName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Kode menu wajib diisi.")]
            [Display(Name = "Kode Menu")]
            [StringLength(40)]
            public string MenuCode { get; set; } = string.Empty;

            [Display(Name = "Ikon (contoh: bi-speedometer2)")]
            [StringLength(120)]
            public string IconKey { get; set; } = "bi-circle";

            [Display(Name = "Alamat Halaman")]
            [StringLength(250)]
            public string UrlPath { get; set; } = "#";

            [Display(Name = "Buka Tab Baru")]
            public bool OpenInNewTab { get; set; }

            [Display(Name = "Sembunyikan")]
            public bool IsHidden { get; set; }

            [Display(Name = "Urutan"), Range(0, 999)]
            public int SortOrder { get; set; } = 10;

            [Display(Name = "Kategori Startup")]
            [StringLength(50)]
            public string StartupCategory { get; set; } = "general";

            public MenuEditRequest ToRequest()
            {
                return new MenuEditRequest
                {
                    DisplayName = DisplayName,
                    MenuCode = MenuCode,
                    IconKey = IconKey,
                    UrlPath = UrlPath,
                    OpenInNewTab = OpenInNewTab,
                    IsHidden = IsHidden,
                    SortOrder = SortOrder,
                    StartupCategory = StartupCategory
                };
            }
        }

        public class SubMenuFormModel : MenuFormModel
        {
            [Required(ErrorMessage = "Menu induk wajib dipilih.")]
            [Display(Name = "Menu Induk")]
            public int ParentMenuId { get; set; }

            public new MenuEditRequest ToRequest()
            {
                var request = base.ToRequest();
                request.ParentId = ParentMenuId;
                return request;
            }
        }
    }
}
