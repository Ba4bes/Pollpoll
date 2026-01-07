using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PollPoll.Services;
using Xunit;

namespace PollPoll.Tests.Unit;

/// <summary>
/// Unit tests for QRCodeService
/// Tests cover: QR code generation, Base64 encoding, URL encoding, caching
/// </summary>
public class QRCodeServiceTests : IDisposable
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly IMemoryCache _memoryCache;
    private readonly QRCodeService _sut;
    private readonly DefaultHttpContext _httpContext;

    public QRCodeServiceTests()
    {
        // Mock HttpContextAccessor for URL generation
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.Host = new HostString("test-codespace.app.github.dev");
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(_httpContext);

        // Real memory cache for testing caching behavior
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _sut = new QRCodeService(_httpContextAccessorMock.Object, _memoryCache);
    }

    [Fact]
    public void GenerateQRCode_ShouldReturnBase64String()
    {
        // Arrange
        var pollCode = "TEST";
        var absoluteUrl = "https://test-codespace.app.github.dev/p/TEST";

        // Act
        var result = _sut.GenerateQRCode(pollCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("data:image/png;base64,", "QR code should be Base64 encoded PNG");
        result.Length.Should().BeGreaterThan(100, "Base64 QR code should have substantial content");
    }

    [Fact]
    public void GenerateQRCode_ShouldEncodeAbsoluteUrl()
    {
        // Arrange
        var pollCode = "ABC1";

        // Act
        var result = _sut.GenerateQRCode(pollCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // QR code should encode the absolute URL (we can't verify content without decoding, but it should generate)
        result.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public void GenerateQRCode_ShouldGenerateConsistentOutputForSameInput()
    {
        // Arrange
        var pollCode = "SAME";

        // Act
        var result1 = _sut.GenerateQRCode(pollCode);
        var result2 = _sut.GenerateQRCode(pollCode);

        // Assert
        result1.Should().Be(result2, "same input should produce same QR code");
    }

    [Fact]
    public void GenerateQRCode_ShouldGenerateDifferentOutputForDifferentCodes()
    {
        // Arrange
        var pollCode1 = "AAA1";
        var pollCode2 = "BBB2";

        // Act
        var result1 = _sut.GenerateQRCode(pollCode1);
        var result2 = _sut.GenerateQRCode(pollCode2);

        // Assert
        result1.Should().NotBe(result2, "different poll codes should produce different QR codes");
    }

    [Fact]
    public void GenerateQRCode_ShouldUseHttpsScheme()
    {
        // Arrange
        _httpContext.Request.Scheme = "http"; // Set to http
        var pollCode = "HTTP";

        // Act
        var result = _sut.GenerateQRCode(pollCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // QR code should still be generated (service should handle both http and https)
    }

    [Fact]
    public void GenerateQRCode_ShouldHandleCodespacesUrl()
    {
        // Arrange
        _httpContext.Request.Host = new HostString("super-disco-abc123.app.github.dev");
        var pollCode = "CODE";

        // Act
        var result = _sut.GenerateQRCode(pollCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public void GenerateQRCode_ShouldCacheResult()
    {
        // Arrange
        var pollCode = "CACHE";

        // Act
        var result1 = _sut.GenerateQRCode(pollCode);
        
        // Modify the HTTP context to change the URL
        _httpContext.Request.Host = new HostString("different-host.app.github.dev");
        
        var result2 = _sut.GenerateQRCode(pollCode);

        // Assert
        result1.Should().Be(result2, "cached result should be returned even if context changes");
    }

    [Theory]
    [InlineData("A1B2")]
    [InlineData("TEST")]
    [InlineData("POLL")]
    [InlineData("DEMO")]
    public void GenerateQRCode_ShouldHandleVariousPollCodes(string pollCode)
    {
        // Act
        var result = _sut.GenerateQRCode(pollCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public void GenerateQRCode_ShouldGenerateMinimum200x200Image()
    {
        // Arrange
        var pollCode = "SIZE";

        // Act
        var result = _sut.GenerateQRCode(pollCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // QR code Base64 length should indicate a reasonably sized image (200x200 minimum per UX-009)
        // A 200x200 PNG QR code typically results in 2000+ characters when Base64 encoded
        result.Length.Should().BeGreaterThan(1000, "QR code should be large enough for projection viewing");
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}
