using Leno.SharedKernel.Exceptions;

namespace Leno.Inventory.Domain.Exceptions;

/// <summary>
/// 库存域业务异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// </summary>
public sealed class InventoryDomainException : DomainException
{
    public InventoryDomainException(string message, string errorCode = "INVENTORY_DOMAIN_ERROR")
        : base(message, errorCode)
    {
    }

    public InventoryDomainException(string message, Exception innerException, string errorCode = "INVENTORY_DOMAIN_ERROR")
        : base(message, innerException, errorCode)
    {
    }
}
