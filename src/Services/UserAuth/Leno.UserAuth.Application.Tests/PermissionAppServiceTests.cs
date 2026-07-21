using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Moq;

namespace Leno.UserAuth.Application.Tests;

/// <summary>
/// PermissionAppService 单元测试，聚焦角色 CRUD 与权限替换的审计日志写入验证。
/// 确保所有写操作在事务内写入 AuditLog，operatorId 可追溯。
/// </summary>
public class PermissionAppServiceTests
{
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly PermissionAppService _sut;
    private readonly Guid _operatorId = Guid.NewGuid();

    public PermissionAppServiceTests()
    {
        _sut = new PermissionAppService(
            _permissionRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    private static Role CreateRole(Guid? id = null, string name = "Manager", bool isBuiltIn = false)
    {
        return Role.Create(id ?? Guid.NewGuid(), name, "Role description", isBuiltIn);
    }

    private static SaveRoleDto CreateSaveRoleDto(string name = "Manager", string? description = "Store manager")
    {
        return new SaveRoleDto { Name = name, Description = description };
    }

    private static UpdatePermissionsDto CreateUpdatePermissionsDto(params string[] permissions)
    {
        return new UpdatePermissionsDto { Permissions = permissions.Length > 0 ? permissions.ToList() : new List<string> { "ui:admin:read" } };
    }

    #region CreateRoleAsync

    [Fact]
    public async Task CreateRoleAsync_Should_Write_AuditLog()
    {
        // Arrange
        _permissionRepositoryMock.Setup(r => r.GetByNameAsync("Manager", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        // Act
        await _sut.CreateRoleAsync(CreateSaveRoleDto(), _operatorId, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "RoleCreate" &&
            log.ResourceType == "Role" &&
            log.OperatorId == _operatorId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRoleAsync_Should_Call_SaveEntitiesAsync()
    {
        // Arrange
        _permissionRepositoryMock.Setup(r => r.GetByNameAsync("Manager", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        // Act
        await _sut.CreateRoleAsync(CreateSaveRoleDto(), _operatorId, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateRoleAsync

    [Fact]
    public async Task UpdateRoleAsync_Should_Write_AuditLog()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = CreateRole(roleId);
        _permissionRepositoryMock.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _permissionRepositoryMock.Setup(r => r.GetByNameAsync("Manager", It.IsAny<CancellationToken>()))
            .ReturnsAsync(role); // same role, name unchanged

        // Act
        await _sut.UpdateRoleAsync(roleId, CreateSaveRoleDto(), _operatorId, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "RoleUpdate" &&
            log.ResourceType == "Role" &&
            log.OperatorId == _operatorId &&
            log.ResourceId == roleId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeleteRoleAsync

    [Fact]
    public async Task DeleteRoleAsync_Should_Write_AuditLog()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = CreateRole(roleId, isBuiltIn: false);
        _permissionRepositoryMock.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _permissionRepositoryMock.Setup(r => r.HasUserReferencesAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.DeleteRoleAsync(roleId, _operatorId, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "RoleDelete" &&
            log.ResourceType == "Role" &&
            log.OperatorId == _operatorId &&
            log.ResourceId == roleId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateRolePermissionsAsync

    [Fact]
    public async Task UpdateRolePermissionsAsync_Should_Write_AuditLog()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = CreateRole(roleId);
        _permissionRepositoryMock.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        // Act
        await _sut.UpdateRolePermissionsAsync(roleId, CreateUpdatePermissionsDto("ui:admin:read", "ui:admin:write"), _operatorId, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "RolePermissionsUpdate" &&
            log.ResourceType == "Role" &&
            log.OperatorId == _operatorId &&
            log.ResourceId == roleId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
