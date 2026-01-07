using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace PollPoll.Services;

/// <summary>
/// Service for generating QR codes for poll join URLs
/// </summary>
public class QRCodeService
{
    /// <summary>
    /// Generates a QR code as a Base64-encoded PNG data URL
    /// </summary>
    /// <param name="content">Content to encode in QR code (typically a URL)</param>
    /// <returns>Data URL string (data:image/png;base64,...)</returns>
    public string GenerateQRCode(string content)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        
        var qrCodeBytes = qrCode.GetGraphic(20); // 20 pixels per module
        var base64 = Convert.ToBase64String(qrCodeBytes);
        
        return $"data:image/png;base64,{base64}";
    }
}
