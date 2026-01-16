namespace one_db_mitra.Models.Menu
{
    public record MenuOperationResult(bool Success, string? Message = null, MenuItem? Menu = null)
    {
        public static MenuOperationResult Ok(MenuItem menu) => new(true, null, menu);
        public static MenuOperationResult Fail(string message) => new(false, message, null);
    }
}
