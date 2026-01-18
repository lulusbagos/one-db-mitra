
# One DB Mitra

Aplikasi manajemen data karyawan dan organisasi berbasis ASP.NET Core MVC.

## Fitur Utama
- Manajemen data karyawan, organisasi, dan user
- Import/export data
- Audit log aktivitas
- Notifikasi real-time
- Hak akses dan manajemen menu
- Dashboard interaktif

## Cara Menjalankan
1. Pastikan sudah terinstall .NET SDK minimal versi 6.0 atau sesuai project
2. Clone repository ini:
   ```bash
   git clone https://github.com/lulusbagos/one-db-mitra.git
   ```
3. Masuk ke folder project:
   ```bash
   cd one-db-mitra
   ```
4. Copy file konfigurasi:
   ```bash
   cp appsettings.Development.json.example appsettings.Development.json
   cp appsettings.json.example appsettings.json
   ```
5. Edit file `appsettings.json` dan `appsettings.Development.json` sesuai konfigurasi database Anda
6. Jalankan aplikasi:
   ```bash
   dotnet run
   ```

## Struktur Folder Penting
- `Controllers/` : Logic aplikasi (MVC Controller)
- `Models/`      : Model data dan ViewModel
- `Views/`       : Tampilan (Razor Pages)
- `Services/`    : Layanan bisnis dan helper
- `wwwroot/`     : Static files (CSS, JS, gambar, dll)

## Kontribusi
1. Fork repository ini
2. Buat branch fitur/bugfix baru
3. Pull request ke branch main

## Catatan Keamanan
- File konfigurasi seperti `appsettings.json` dan `appsettings.Development.json` sudah di-ignore agar tidak bocor ke publik.
- Pastikan data sensitif tidak di-commit ke repository.

## Lisensi
Tulis lisensi project di sini jika ada.
