using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using one_db_mitra.Models.Menu;

namespace one_db_mitra.Services.Menu
{
    public interface IMenuRepository
    {
        Task<IReadOnlyList<MenuItem>> GetMenuTreeAsync(MenuScope scope, CancellationToken cancellationToken = default);
        Task<MenuOperationResult> AddMenuAsync(MenuEditRequest request, CancellationToken cancellationToken = default);
        Task<MenuOperationResult> AddSubMenuAsync(int parentMenuId, MenuEditRequest request, CancellationToken cancellationToken = default);
        Task<MenuOperationResult> UpdateMenuFlagsAsync(int menuId, bool isHidden, bool openInNewTab, CancellationToken cancellationToken = default);
    }
}
