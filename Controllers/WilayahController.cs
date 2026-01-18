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
        var urls = new[]
        {
            "https://emsifa.github.io/api-wilayah-indonesia/api/provinces.json",
            "https://raw.githubusercontent.com/emsifa/api-wilayah-indonesia/master/api/provinces.json",
            "https://wilayah.id/api/provinces.json"
        };
        return await ProxyAsync(urls, cancellationToken);
    }

    [HttpGet("Kabupaten/{provinsiId}")]
    public async Task<IActionResult> Kabupaten(string provinsiId, CancellationToken cancellationToken)
    {
        var urls = new[]
        {
            $"https://emsifa.github.io/api-wilayah-indonesia/api/regencies/{provinsiId}.json",
            $"https://raw.githubusercontent.com/emsifa/api-wilayah-indonesia/master/api/regencies/{provinsiId}.json",
            $"https://wilayah.id/api/regencies/{provinsiId}.json"
        };
        return await ProxyAsync(urls, cancellationToken);
    }

    [HttpGet("Kecamatan/{kabupatenId}")]
    public async Task<IActionResult> Kecamatan(string kabupatenId, CancellationToken cancellationToken)
    {
        var urls = new[]
        {
            $"https://emsifa.github.io/api-wilayah-indonesia/api/districts/{kabupatenId}.json",
            $"https://raw.githubusercontent.com/emsifa/api-wilayah-indonesia/master/api/districts/{kabupatenId}.json",
            $"https://wilayah.id/api/districts/{kabupatenId}.json"
        };
        return await ProxyAsync(urls, cancellationToken);
    }

    [HttpGet("Desa/{kecamatanId}")]
    public async Task<IActionResult> Desa(string kecamatanId, CancellationToken cancellationToken)
    {
        var urls = new[]
        {
            $"https://emsifa.github.io/api-wilayah-indonesia/api/villages/{kecamatanId}.json",
            $"https://raw.githubusercontent.com/emsifa/api-wilayah-indonesia/master/api/villages/{kecamatanId}.json",
            $"https://wilayah.id/api/villages/{kecamatanId}.json"
        };
        return await ProxyAsync(urls, cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(IEnumerable<string> urls, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        if (!client.DefaultRequestHeaders.UserAgent.Any())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("one-db-mitra/1.0");
        }

        foreach (var url in urls)
        {
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                return Content(payload, "application/json");
            }
            catch (HttpRequestException)
            {
                // Try next fallback URL.
            }
        }
        return Content("[]", "application/json");
    }
}
