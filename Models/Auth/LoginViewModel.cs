using System.ComponentModel.DataAnnotations;

namespace one_db_mitra.Models.Auth
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username wajib diisi.")]
        [Display(Name = "No NIK / Email")]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password wajib diisi.")]
        [StringLength(200)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ingat saya")]
        public bool RememberMe { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
