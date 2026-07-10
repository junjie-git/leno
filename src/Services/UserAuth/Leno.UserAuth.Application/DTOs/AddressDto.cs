using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 收货地址 DTO。
/// </summary>
public sealed class AddressDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string RecipientName { get; init; } = string.Empty;

    public string RecipientPhone { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string District { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string? Tag { get; init; }

    public bool IsDefault { get; init; }

    public AddressStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 新增/修改地址请求 DTO。
/// </summary>
public sealed class SaveAddressDto
{
    public string RecipientName { get; init; } = string.Empty;

    public string RecipientPhone { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string District { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string? Tag { get; init; }

    public bool IsDefault { get; init; }
}
