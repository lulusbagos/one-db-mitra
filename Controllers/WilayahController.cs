using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace one_db_mitra.Controllers
{
    [Route("api/wilayah")]
    public class WilayahController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public WilayahController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("provinsi")]
        public async Task<IActionResult> Provinsi(CancellationToken cancellationToken)
        {
            return await ProxyAsync("https://www.emsifa.com/api-wilayah-indonesia/api/provinces.json", cancellationToken);
        }

        [HttpGet("kabupaten")]
        public async Task<IActionResult> Kabupaten(string provinsiId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(provinsiId))
            {
                return BadRequest(new { message = "provinsiId wajib diisi." });
            }

            var url = $"https://www.emsifa.com/api-wilayah-indonesia/api/regencies/{provinsiId}.json";
            return await ProxyAsync(url, cancellationToken);
        }

        [HttpGet("kecamatan")]
        public async Task<IActionResult> Kecamatan(string kabupatenId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(kabupatenId))
            {
                return BadRequest(new { message = "kabupatenId wajib diisi." });
            }

            var url = $"https://www.emsifa.com/api-wilayah-indonesia/api/districts/{kabupatenId}.json";
            return await ProxyAsync(url, cancellationToken);
        }

        [HttpGet("desa")]
        public async Task<IActionResult> Desa(string kecamatanId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(kecamatanId))
            {
                return BadRequest(new { message = "kecamatanId wajib diisi." });
            }

            var url = $"https://www.emsifa.com/api-wilayah-indonesia/api/villages/{kecamatanId}.json";
            return await ProxyAsync(url, cancellationToken);
        }

        private async Task<IActionResult> ProxyAsync(string url, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new { message = "Gagal mengambil data wilayah." });
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return Content(json, "application/json");
        }
    }
}
