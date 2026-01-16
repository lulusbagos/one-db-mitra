using one_db_mitra.Services.Menu;
using one_db_mitra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using one_db_mitra.Services.Audit;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Security.Claims;
using one_db_mitra.Hubs;
using one_db_mitra.Services.AppSetting;
using one_db_mitra.Services.Email;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddSignalR();
builder.Services.AddScoped<AppSettingService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<OneDbMitraContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PrimarySqlServer")));

builder.Services.AddScoped<IMenuRepository, DbMenuRepository>();
builder.Services.AddScoped<MenuProfileService>();
builder.Services.AddHostedService<EmailBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var sessionIdValue = context.User.FindFirst("session_id")?.Value;
        if (int.TryParse(sessionIdValue, out var sessionId))
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneDbMitraContext>();
            var session = await db.tbl_r_sesi_aktif.FirstOrDefaultAsync(s => s.sesi_id == sessionId);
            if (session is null || !session.is_aktif)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Account/Login");
                return;
            }

            session.last_seen = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=MenuAdmin}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();
