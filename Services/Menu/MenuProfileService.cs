using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using one_db_mitra.Models.Menu;

namespace one_db_mitra.Services.Menu
{
    public class MenuProfileService
    {
        private readonly IMemoryCache _cache;
        private readonly IMenuRepository _repository;
        private readonly TimeSpan _cacheDuration;

        public MenuProfileService(IMemoryCache cache, IMenuRepository repository, IConfiguration configuration)
        {
            _cache = cache;
            _repository = repository;
            var minutes = configuration.GetValue<int?>("MenuOptions:SessionCacheMinutes") ?? 20;
            _cacheDuration = TimeSpan.FromMinutes(minutes);
        }

        public Task<IReadOnlyList<MenuItem>> GetMenusForSessionAsync(string sessionKey, MenuScope scope, CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue($"menu::{sessionKey}", out IReadOnlyList<MenuItem>? cached) && cached is not null)
            {
                return Task.FromResult(cached);
            }

            return LoadAndCacheAsync(sessionKey, scope, cancellationToken);
        }

        public void InvalidateSessionMenu(string sessionKey)
        {
            _cache.Remove($"menu::{sessionKey}");
        }

        public static string BuildSessionKey(string userId, string companyId, string kategori)
        {
            return $"{userId}:{companyId}:{kategori}".ToLowerInvariant();
        }

        private async Task<IReadOnlyList<MenuItem>> LoadAndCacheAsync(string sessionKey, MenuScope scope, CancellationToken cancellationToken)
        {
            var menus = await _repository.GetMenuTreeAsync(scope, cancellationToken);
            _cache.Set($"menu::{sessionKey}", menus, _cacheDuration);
            return menus;
        }
    }
}
