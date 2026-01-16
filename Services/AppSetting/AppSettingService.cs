using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using one_db_mitra.Data;
using one_db_mitra.Models.Db;

namespace one_db_mitra.Services.AppSetting
{
    public class AppSettingService
    {
        private const string CacheKey = "app_setting_default";
        private readonly OneDbMitraContext _context;
        private readonly IMemoryCache _cache;

        public AppSettingService(OneDbMitraContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<tbl_m_setting_aplikasi> GetAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out tbl_m_setting_aplikasi cached))
            {
                return cached;
            }

            var setting = await _context.tbl_m_setting_aplikasi.AsNoTracking()
                .OrderBy(s => s.setting_id)
                .FirstOrDefaultAsync(cancellationToken)
                ?? new tbl_m_setting_aplikasi
                {
                    nama_aplikasi = "One DB Mitra",
                    nama_header = "Konsol Administrasi",
                    footer_text = "Copyright System Integrasi Departemen supported by PT INDEXIM COALINDO",
                    logo_url = null,
                    announcement_enabled = false,
                    announcement_type = "info",
                    font_primary = "Poppins",
                    font_secondary = "Manrope"
                };

            _cache.Set(CacheKey, setting, TimeSpan.FromMinutes(5));
            return setting;
        }

        public void Invalidate()
        {
            _cache.Remove(CacheKey);
        }
    }
}
