using Leno.SharedKernel.Exceptions;

namespace Leno.Product.Application.Exceptions;

/// <summary>
/// 应用层输入校验异常，继承领域异常基类以经全局异常中间件统一映射为 400。
/// </summary>
public sealed class ProductValidationException : DomainException
{
    public ProductValidationException(string message)
        : base(message, "PRODUCT_VALIDATION_ERROR")
    {
    }

    public ProductValidationException(IEnumerable<string> errors)
        : base(string.Join(" | ", errors), "PRODUCT_VALIDATION_ERROR")
    {
    }
}
