using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace one_db_mitra.Controllers;

[Route("Wilayah")]
public class WilayahController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public WilayahController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("Provinsi")]
    public async Task<IActionResult> Provinsi(CancellationToken cancellationToken)
    {
        return await ProxyAsync("https://emsifa.github.io/api-wilayah-indonesia/api/provinces.json", cancellationToken);
    }

    [HttpGet("Kabupaten/{provinsiId}")]
    public async Task<IActionResult> Kabupaten(string provinsiId, CancellationToken cancellationToken)
    {
        return await ProxyAsync($"https://emsifa.github.io/api-wilayah-indonesia/api/regencies/{provinsiId}.json", cancellationToken);
    }

    [HttpGet("Kecamatan/{kabupatenId}")]
    public async Task<IActionResult> Kecamatan(string kabupatenId, CancellationToken cancellationToken)
    {
        return await ProxyAsync($"https://emsifa.github.io/api-wilayah-indonesia/api/districts/{kabupatenId}.json", cancellationToken);
    }

    [HttpGet("Desa/{kecamatanId}")]
    public async Task<IActionResult> Desa(string kecamatanId, CancellationToken cancellationToken)
    {
        return await ProxyAsync($"https://emsifa.github.io/api-wilayah-indonesia/api/villages/{kecamatanId}.json", cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Content("[]", "application/json");
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Content("[]", "application/json");
        }
        return Content(payload, "application/json");
    }
}
