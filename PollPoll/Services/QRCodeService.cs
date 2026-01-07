using QRCoder;
using Microsoft.Extensions.Caching.Memory;

namespace PollPoll.Services;

/// <summary>
/// Service for generating QR codes for poll join URLs
/// </summary>
public class QRCodeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private const int QR_SIZE_PIXELS = 200;
    private const int CACHE_DURATION_MINUTES = 60;

    public QRCodeService(IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
    {
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
    }

    /// <summary>
    /// Generates a QR code as a Base64-encoded PNG data URL for a poll's voting page
    /// </summary>
    /// <param name="pollCode">The 6-character poll code</param>
    /// <returns>Base64-encoded PNG data URL (data:image/png;base64,...)</returns>
    public string GenerateQRCode(string pollCode)
    {
        // Check cache first (per PERF-008)
        string cacheKey = $"qr_{pollCode}";
        if (_cache.TryGetValue(cacheKey, out string? cachedQrCode) && cachedQrCode != null)
        {
            return cachedQrCode;
        }

        // Build absolute URL for voting page
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        string baseUrl = $"{request.Scheme}://{request.Host}";
        string votingUrl = $"{baseUrl}/p/{pollCode}";

        // Generate QR code using QRCoder library (minimum 200x200px per UX-009)
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(votingUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        
        byte[] qrCodeImage = qrCode.GetGraphic(20); // 20 pixels per module for larger output
        string base64Image = Convert.ToBase64String(qrCodeImage);
        string dataUrl = $"data:image/png;base64,{base64Image}";

        // Cache the result (per PERF-008)
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
        _cache.Set(cacheKey, dataUrl, cacheOptions);

        return dataUrl;
    }
}
