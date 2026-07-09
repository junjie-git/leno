using System.Text.Json;
using System.Text.Json.Serialization;

namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// <see cref="Money"/> 值对象的 System.Text.Json 序列化转换器。
/// 同时提供静态序列化/反序列化方法，供 EF Core 值转换器在基础设施层复用（避免共享内核引用 EF Core）。
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    private const string AmountName = "amount";
    private const string CurrencyName = "currency";

    public override Money? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Money 必须为 JSON 对象");
        }

        decimal amount = 0m;
        string? currency = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();
            switch (propertyName)
            {
                case AmountName:
                    amount = reader.GetDecimal();
                    break;
                case CurrencyName:
                    currency = reader.GetString();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new JsonException("Money 缺少 currency 字段");
        }

        return Money.Create(amount, currency!);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(AmountName, value.Amount);
        writer.WriteString(CurrencyName, value.Currency);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 将 <see cref="Money"/> 序列化为字符串，供 EF Core 值转换器（<c>ValueConverter&lt;Money, string&gt;</c>）使用。
    /// </summary>
    public static string ToStorage(Money money)
        => $"{money.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{money.Currency}";

    /// <summary>
    /// 从存储字符串反序列化为 <see cref="Money"/>，供 EF Core 值转换器使用。
    /// </summary>
    public static Money FromStorage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("存储值不可为空", nameof(value));
        }

        var parts = value.Split('|');
        if (parts.Length != 2)
        {
            throw new FormatException("Money 存储值格式错误，须为 amount|currency");
        }

        var amount = decimal.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        return Money.Create(amount, parts[1]);
    }
}
