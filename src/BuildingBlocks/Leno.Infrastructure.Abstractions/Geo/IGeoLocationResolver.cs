namespace Leno.Infrastructure.Abstractions.Geo;

/// <summary>
/// 地理定位解析抽象：内网 IP 标记为「内网·本地」，公网 IP 通过 MaxMind GeoLite2 本地库查询。
/// 实现位于 Leno.Infrastructure（MaxMindGeoLocationResolver）。
/// </summary>
public interface IGeoLocationResolver
{
    GeoLocation Resolve(string ipAddress);
}
