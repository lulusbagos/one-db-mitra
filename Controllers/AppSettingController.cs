using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using one_db_mitra.Data;
using one_db_mitra.Models.Admin;
using one_db_mitra.Models.Db;
using one_db_mitra.Services.AppSetting;

namespace one_db_mitra.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class AppSettingController : Controller
    {
        private readonly OneDbMitraContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly AppSettingService _settingService;

        public AppSettingController(OneDbMitraContext context, IWebHostEnvironment environment, AppSettingService settingService)
        {
            _context = context;
            _environment = environment;
            _settingService = settingService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var setting = await _context.tbl_m_setting_aplikasi.AsNoTracking()
                .OrderBy(s => s.setting_id)
                .FirstOrDefaultAsync(cancellationToken);

            var model = new AppSettingViewModel
            {
                SettingId = setting?.setting_id ?? 0,
                AppName = setting?.nama_aplikasi,
                HeaderName = setting?.nama_header,
                FooterText = setting?.footer_text,
                LogoUrl = setting?.logo_url,
                AnnouncementEnabled = setting?.announcement_enabled ?? false,
                AnnouncementTitle = setting?.announcement_title,
                AnnouncementMessage = setting?.announcement_message,
                AnnouncementType = setting?.announcement_type,
                AnnouncementStart = setting?.announcement_start,
                AnnouncementEnd = setting?.announcement_end,
                FontPrimary = setting?.font_primary,
                FontSecondary = setting?.font_secondary
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(AppSettingViewModel model, IFormFile? logoFile, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var setting = await _context.tbl_m_setting_aplikasi
                .FirstOrDefaultAsync(s => s.setting_id == model.SettingId, cancellationToken);

            if (setting is null)
            {
                setting = new tbl_m_setting_aplikasi
                {
                    dibuat_pada = DateTime.UtcNow
                };
                _context.tbl_m_setting_aplikasi.Add(setting);
            }

            if (logoFile != null && logoFile.Length > 0)
            {
                var extension = Path.GetExtension(logoFile.FileName);
                var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
                var folder = Path.Combine(_environment.WebRootPath, "uploads", "logo");
                Directory.CreateDirectory(folder);
                var filePath = Path.Combine(folder, fileName);

                await using var stream = System.IO.File.Create(filePath);
                await logoFile.CopyToAsync(stream, cancellationToken);

                setting.logo_url = $"/uploads/logo/{fileName}";
            }
            else
            {
                setting.logo_url = string.IsNullOrWhiteSpace(model.LogoUrl) ? setting.logo_url : model.LogoUrl?.Trim();
            }

            setting.nama_aplikasi = string.IsNullOrWhiteSpace(model.AppName) ? setting.nama_aplikasi : model.AppName?.Trim();
            setting.nama_header = string.IsNullOrWhiteSpace(model.HeaderName) ? setting.nama_header : model.HeaderName?.Trim();
            setting.footer_text = string.IsNullOrWhiteSpace(model.FooterText) ? setting.footer_text : model.FooterText?.Trim();
            setting.announcement_enabled = model.AnnouncementEnabled;
            setting.announcement_title = model.AnnouncementTitle?.Trim();
            setting.announcement_message = model.AnnouncementMessage?.Trim();
            setting.announcement_type = string.IsNullOrWhiteSpace(model.AnnouncementType) ? "info" : model.AnnouncementType.Trim().ToLowerInvariant();
            setting.announcement_start = model.AnnouncementStart;
            setting.announcement_end = model.AnnouncementEnd;
            setting.font_primary = string.IsNullOrWhiteSpace(model.FontPrimary) ? "Poppins" : model.FontPrimary.Trim();
            setting.font_secondary = string.IsNullOrWhiteSpace(model.FontSecondary) ? "Manrope" : model.FontSecondary.Trim();
            setting.diubah_pada = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            _settingService.Invalidate();

            TempData["AlertMessage"] = "Pengaturan aplikasi berhasil diperbarui.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }
    }
}
