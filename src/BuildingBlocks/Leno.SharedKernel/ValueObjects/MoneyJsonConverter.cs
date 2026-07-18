using System.Text.Json;
using System.Text.Json.Serialization;

namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// <see cref="Money"/> 值对象的 System.Text.Json 序列化转换器。
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
}
