using Leno.SharedKernel.Exceptions;

namespace Leno.Product.Domain.Exceptions;

/// <summary>
/// 商品域领域异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// </summary>
public sealed class ProductDomainException : DomainException
{
    public ProductDomainException(string message, string errorCode = "PRODUCT_DOMAIN_ERROR")
        : base(message, errorCode)
    {
    }

    public ProductDomainException(string message, Exception innerException, string errorCode = "PRODUCT_DOMAIN_ERROR")
        : base(message, innerException, errorCode)
    {
    }
}
