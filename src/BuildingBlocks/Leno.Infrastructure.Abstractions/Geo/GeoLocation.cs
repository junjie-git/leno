namespace Leno.Infrastructure.Abstractions.Geo;

/// <summary>地理定位结果。</summary>
public sealed class GeoLocation
{
    public string Country { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public override string ToString()
        => string.IsNullOrEmpty(City) ? $"{Country}" : $"{Country}·{Province}·{City}";
}
