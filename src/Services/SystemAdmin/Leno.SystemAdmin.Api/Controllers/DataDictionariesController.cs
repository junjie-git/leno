using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 数据字典管理控制器（运营端 CRUD、字典项管理与公开编码查询）。
/// </summary>
[ApiController]
public sealed class DataDictionariesController : SystemAdminControllerBase
{
    private readonly IDataDictionaryAppService _dictionaryAppService;

    public DataDictionariesController(ICurrentUserContext currentUser, IDataDictionaryAppService dictionaryAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(dictionaryAppService);
        _dictionaryAppService = dictionaryAppService;
    }

    /// <summary>分页查询数据字典，支持名称与状态过滤。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/dictionaries")]
    [ProducesResponseType(typeof(ApiResponse<DataDictionaryListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? name,
        [FromQuery] DictionaryStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dictionaryAppService.QueryAsync(name, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>创建数据字典。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/dictionaries")]
    [ProducesResponseType(typeof(ApiResponse<DataDictionaryDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveDataDictionaryDto dto, CancellationToken ct)
    {
        var result = await _dictionaryAppService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetByCodeAsync), new { code = result.Code }, ApiResponse.Success(result));
    }

    /// <summary>更新数据字典（编码不可变）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/dictionaries/{dictionaryId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DataDictionaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid dictionaryId, [FromBody] SaveDataDictionaryDto dto, CancellationToken ct)
    {
        var result = await _dictionaryAppService.UpdateAsync(dictionaryId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>启用字典。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/dictionaries/{dictionaryId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid dictionaryId, CancellationToken ct)
    {
        await _dictionaryAppService.EnableAsync(dictionaryId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用字典。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/dictionaries/{dictionaryId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid dictionaryId, CancellationToken ct)
    {
        await _dictionaryAppService.DisableAsync(dictionaryId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>新增字典项。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/dictionaries/{dictionaryId:guid}/items")]
    [ProducesResponseType(typeof(ApiResponse<DataDictionaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddItemAsync(Guid dictionaryId, [FromBody] AddDictionaryItemDto dto, CancellationToken ct)
    {
        var result = await _dictionaryAppService.AddItemAsync(dictionaryId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新字典项。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/dictionaries/{dictionaryId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DataDictionaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItemAsync(Guid dictionaryId, Guid itemId, [FromBody] UpdateDictionaryItemDto dto, CancellationToken ct)
    {
        var result = await _dictionaryAppService.UpdateItemAsync(dictionaryId, itemId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>移除字典项（幂等）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpDelete("api/admin/dictionaries/{dictionaryId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItemAsync(Guid dictionaryId, Guid itemId, CancellationToken ct)
    {
        await _dictionaryAppService.RemoveItemAsync(dictionaryId, itemId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>按编码获取字典（含字典项），公开查询。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpGet("api/dictionaries/{code}")]
    [ProducesResponseType(typeof(ApiResponse<DataDictionaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCodeAsync(string code, CancellationToken ct)
    {
        var result = await _dictionaryAppService.GetByCodeAsync(code, ct);
        return Ok(ApiResponse.Success(result));
    }
}
