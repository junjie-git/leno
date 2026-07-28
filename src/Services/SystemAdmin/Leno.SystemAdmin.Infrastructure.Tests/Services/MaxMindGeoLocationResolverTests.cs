using Leno.SystemAdmin.Infrastructure.Services;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class MaxMindGeoLocationResolverTests
{
    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.5.10")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    public void Resolve_InternalIp_ReturnsInternalMarking(string ip)
    {
        var resolver = new MaxMindGeoLocationResolver(mmdbPath: "/non-existent-path.mmdb");

        var geo = resolver.Resolve(ip);

        geo.Country.Should().Be("内网");
        geo.Province.Should().Be("本地");
    }

    [Fact]
    public void Resolve_EmptyIp_ReturnsUnknown()
    {
        var resolver = new MaxMindGeoLocationResolver(mmdbPath: "/non-existent-path.mmdb");

        var geo = resolver.Resolve("");

        geo.Country.Should().Be("未知");
    }

    [Fact]
    public void Resolve_PublicIpWithoutDb_ReturnsUnknown()
    {
        var resolver = new MaxMindGeoLocationResolver(mmdbPath: "/non-existent-path.mmdb");

        var geo = resolver.Resolve("8.8.8.8");

        geo.Country.Should().Be("未知");
    }

    [Fact]
    public void Resolve_InvalidIp_ReturnsUnknown()
    {
        var resolver = new MaxMindGeoLocationResolver(mmdbPath: "/non-existent-path.mmdb");

        var geo = resolver.Resolve("invalid-ip-string");

        geo.Country.Should().Be("未知");
    }

    [Fact]
    public void Resolve_InternalIp_ToStringContainsInternalMarking()
    {
        var resolver = new MaxMindGeoLocationResolver(mmdbPath: "/non-existent-path.mmdb");

        var geo = resolver.Resolve("10.0.0.1");

        geo.ToString().Should().Contain("内网");
    }
}
