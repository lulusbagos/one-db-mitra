using System;
using System.ComponentModel.DataAnnotations;

namespace one_db_mitra.Models.Auth
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "No NIK wajib diisi.")]
        [StringLength(50)]
        public string NoNik { get; set; } = string.Empty;

        [Required(ErrorMessage = "No KTP wajib diisi.")]
        [StringLength(30)]
        public string NoKtp { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tanggal lahir wajib diisi.")]
        [DataType(DataType.Date)]
        public DateTime? TanggalLahir { get; set; }

        [Required(ErrorMessage = "Email wajib diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;
    }
}
