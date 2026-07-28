using System.Net;
using Leno.Infrastructure.Abstractions.Geo;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// MaxMind GeoLite2 本地库地理定位解析器。
/// 内网 IP（10.0.0.0/8 / 172.16.0.0/12 / 192.168.0.0/16 / 127.0.0.0/8）标记为「内网·本地」；
/// 公网 IP 通过 MaxMind GeoLite2 .mmdb 查询；DB 文件不存在时返回「未知」。
/// </summary>
public sealed class MaxMindGeoLocationResolver : IGeoLocationResolver
{
    private const string InternalCountry = "内网";
    private const string InternalProvince = "本地";
    private const string UnknownCountry = "未知";
    private readonly string _mmdbPath;
    private readonly object _dbLock = new();
    private volatile bool _dbLoaded;
    private volatile bool _dbAvailable;
    private MaxMind.Db.Reader? _reader;

    public MaxMindGeoLocationResolver(string mmdbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mmdbPath);
        _mmdbPath = mmdbPath;
    }

    /// <inheritdoc />
    public GeoLocation Resolve(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return new GeoLocation { Country = UnknownCountry };

        if (!IPAddress.TryParse(ipAddress, out var ip)) return new GeoLocation { Country = UnknownCountry };

        if (IsInternalIp(ip))
        {
            return new GeoLocation { Country = InternalCountry, Province = InternalProvince };
        }

        var reader = GetReader();
        if (reader is null)
        {
            return new GeoLocation { Country = UnknownCountry };
        }

        try
        {
            var response = reader.Find<MaxMind.GeoIP2.Responses.CityResponse>(ip);
            if (response is null)
            {
                return new GeoLocation { Country = UnknownCountry };
            }

            var country = response.Country?.Name ?? UnknownCountry;
            var province = response.MostSpecificSubdivision?.Name ?? string.Empty;
            var city = response.City?.Name ?? string.Empty;
            return new GeoLocation { Country = country, Province = province, City = city };
        }
        catch
        {
            return new GeoLocation { Country = UnknownCountry };
        }
    }

    private static bool IsInternalIp(IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false;

        // 10.0.0.0/8
        if (bytes[0] == 10) return true;
        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168) return true;
        // 127.0.0.0/8
        if (bytes[0] == 127) return true;
        return false;
    }

    private MaxMind.Db.Reader? GetReader()
    {
        if (_dbLoaded) return _reader;
        lock (_dbLock)
        {
            if (_dbLoaded) return _reader;
            _dbLoaded = true;
            if (!File.Exists(_mmdbPath))
            {
                _dbAvailable = false;
                return null;
            }
            try
            {
                _reader = new MaxMind.Db.Reader(_mmdbPath);
                _dbAvailable = true;
            }
            catch
            {
                _dbAvailable = false;
                _reader = null;
            }
            return _reader;
        }
    }
}
