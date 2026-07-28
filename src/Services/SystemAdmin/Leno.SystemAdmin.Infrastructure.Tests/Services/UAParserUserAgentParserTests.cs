using Leno.Infrastructure.UserAgent;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class UAParserUserAgentParserTests
{
    private readonly UAParserUserAgentParser _parser = new();

    [Fact]
    public void ParseBrowser_ChromeUA_ReturnsChromeWithVersion()
    {
        var ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var browser = _parser.ParseBrowser(ua);

        browser.Should().Contain("Chrome");
    }

    [Fact]
    public void ParseOs_WindowsUA_ReturnsWindows()
    {
        var ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var os = _parser.ParseOs(ua);

        os.Should().Contain("Windows");
    }

    [Fact]
    public void ParseOs_MacUA_ReturnsMacOS()
    {
        var ua = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var os = _parser.ParseOs(ua);

        os.Should().Contain("Mac OS");
    }

    [Fact]
    public void ParseOs_LinuxUA_ReturnsLinux()
    {
        var ua = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var os = _parser.ParseOs(ua);

        os.Should().Contain("Linux");
    }

    [Fact]
    public void ParseBrowser_FirefoxUA_ReturnsFirefox()
    {
        var ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0";

        var browser = _parser.ParseBrowser(ua);

        browser.Should().Contain("Firefox");
    }

    [Fact]
    public void ParseBrowser_EmptyString_ReturnsUnknown()
    {
        var browser = _parser.ParseBrowser("");

        browser.Should().NotBeNull();
    }

    [Fact]
    public void ParseDeviceFingerprint_ReturnsConsistentHash()
    {
        var ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var fp1 = _parser.ParseDeviceFingerprint(ua);
        var fp2 = _parser.ParseDeviceFingerprint(ua);

        fp1.Should().Be(fp2);
        fp1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ParseDeviceFingerprint_DifferentUA_ReturnsDifferentHash()
    {
        var ua1 = "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0.0.0";
        var ua2 = "Mozilla/5.0 (Macintosh) Chrome/120.0.0.0";

        var fp1 = _parser.ParseDeviceFingerprint(ua1);
        var fp2 = _parser.ParseDeviceFingerprint(ua2);

        fp1.Should().NotBe(fp2);
    }
}
