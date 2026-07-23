using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Infrastructure.Security;
using Leno.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Tests.Security;

/// <summary>
/// BcryptToArgon2Migrator 单元测试（3.10 安全技术栈升级）。
/// 覆盖 bcrypt→Argon2id 懒迁移、已迁移跳过、OAuth 用户跳过、不可识别格式处理等场景。
/// </summary>
public class BcryptToArgon2MigratorTests
{
    private const string BcryptHash = "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";
    private const string Argon2idHash = "$argon2id$v=19$m=8,t=1,p=1$dGVzdC1zYWx0$dGVzdC1oYXNo";
    private const string NewArgon2idHash = "$argon2id$v=19$m=8,t=1,p=1$bmV3LXNhbHQ$bmV3LWhhc2g";
    private const string PlainPassword = "TestP@ssw0rd";

    [Fact]
    public async Task TryMigrateAsync_With_Bcrypt_Hash_Should_Migrate_To_Argon2id()
    {
        // Arrange
        var user = CreateUser(passwordHash: BcryptHash);
        var (migrator, passwordHasher, userRepo) = CreateMigrator();
        passwordHasher.Setup(h => h.DetectAlgorithm(BcryptHash))
                      .Returns(PasswordHashAlgorithm.Bcrypt);
        passwordHasher.Setup(h => h.HashPassword(PlainPassword))
                      .Returns(NewArgon2idHash);

        // Act
        var result = await migrator.TryMigrateAsync(user, PlainPassword, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        user.PasswordHash.Should().Be(NewArgon2idHash, "bcrypt 哈希应被替换为 Argon2id 哈希");
        user.PasswordHashVersion.Should().Be(1, "迁移后版本应为 1（Argon2id）");
        userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryMigrateAsync_With_Argon2id_Hash_Should_Skip_Migration()
    {
        // Arrange
        var user = CreateUser(passwordHash: Argon2idHash);
        var (migrator, passwordHasher, userRepo) = CreateMigrator();
        passwordHasher.Setup(h => h.DetectAlgorithm(Argon2idHash))
                      .Returns(PasswordHashAlgorithm.Argon2id);

        // Act
        var result = await migrator.TryMigrateAsync(user, PlainPassword, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        user.PasswordHash.Should().Be(Argon2idHash, "已是 Argon2id 时不应修改哈希");
        userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never, "无需迁移时不应调用仓储更新");
        passwordHasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never, "无需迁移时不应重新哈希");
    }

    [Fact]
    public async Task TryMigrateAsync_With_Null_PasswordHash_Should_Skip_Migration()
    {
        // Arrange — 纯 OAuth 用户无密码哈希
        var user = CreateUser(passwordHash: null);
        var (migrator, _, userRepo) = CreateMigrator();

        // Act
        var result = await migrator.TryMigrateAsync(user, PlainPassword, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never, "OAuth 用户无密码哈希时不应迁移");
    }

    [Fact]
    public async Task TryMigrateAsync_With_Empty_PasswordHash_Should_Skip_Migration()
    {
        // Arrange
        var user = CreateUser(passwordHash: string.Empty);
        var (migrator, _, userRepo) = CreateMigrator();

        // Act
        var result = await migrator.TryMigrateAsync(user, PlainPassword, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryMigrateAsync_With_Unrecognized_Hash_Format_Should_Return_False()
    {
        // Arrange
        var user = CreateUser(passwordHash: "$unknown$format$hash");
        var (migrator, passwordHasher, userRepo) = CreateMigrator();
        passwordHasher.Setup(h => h.DetectAlgorithm(It.IsAny<string>()))
                      .Throws(new FormatException("无法识别的格式"));

        // Act
        var result = await migrator.TryMigrateAsync(user, PlainPassword, CancellationToken.None);

        // Assert
        result.Should().BeFalse("不可识别的哈希格式应返回 false 而非抛异常");
        userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never, "格式错误时不应迁移");
    }

    [Fact]
    public async Task TryMigrateAsync_With_Null_User_Should_Throw()
    {
        var (migrator, _, _) = CreateMigrator();

        var act = async () => await migrator.TryMigrateAsync(null!, PlainPassword, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TryMigrateAsync_Should_Call_DetectAlgorithm_To_Determine_Algorithm()
    {
        // Arrange
        var user = CreateUser(passwordHash: BcryptHash);
        var (migrator, passwordHasher, _) = CreateMigrator();
        passwordHasher.Setup(h => h.DetectAlgorithm(BcryptHash))
                      .Returns(PasswordHashAlgorithm.Bcrypt);
        passwordHasher.Setup(h => h.HashPassword(PlainPassword))
                      .Returns(NewArgon2idHash);

        // Act
        await migrator.TryMigrateAsync(user, PlainPassword, CancellationToken.None);

        // Assert
        passwordHasher.Verify(h => h.DetectAlgorithm(BcryptHash), Times.Once, "应调用 DetectAlgorithm 判断当前算法");
    }

    [Fact]
    public async Task TryMigrateAsync_Should_Log_Information_After_Successful_Migration()
    {
        // Arrange
        var user = CreateUser(passwordHash: BcryptHash);
        var logger = new Mock<ILogger<BcryptToArgon2Migrator>>();
        var passwordHasher = new Mock<IPasswordHasher>();
        var userRepo = new Mock<IUserRepository>();

        passwordHasher.Setup(h => h.DetectAlgorithm(BcryptHash))
                      .Returns(PasswordHashAlgorithm.Bcrypt);
        passwordHasher.Setup(h => h.HashPassword(PlainPassword))
                      .Returns(NewArgon2idHash);

        var migrator = new BcryptToArgon2Migrator(passwordHasher.Object, userRepo.Object, logger.Object);

        // Act
        await migrator.TryMigrateAsync(user, PlainPassword, CancellationToken.None);

        // Assert
        logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "迁移成功后应记录 Information 日志");
    }

    /// <summary>创建 BcryptToArgon2Migrator 及其 Mock 依赖。</summary>
    private static (BcryptToArgon2Migrator migrator, Mock<IPasswordHasher> passwordHasher, Mock<IUserRepository> userRepo) CreateMigrator()
    {
        var passwordHasher = new Mock<IPasswordHasher>();
        var userRepo = new Mock<IUserRepository>();
        var logger = new Mock<ILogger<BcryptToArgon2Migrator>>();
        var migrator = new BcryptToArgon2Migrator(passwordHasher.Object, userRepo.Object, logger.Object);
        return (migrator, passwordHasher, userRepo);
    }

    /// <summary>创建测试用户，passwordHash 为 null 时使用 email 作为登录方式。</summary>
    private static User CreateUser(string? passwordHash)
    {
        return User.Create(
            id: Guid.NewGuid(),
            username: "testuser_" + Guid.NewGuid().ToString("N")[..8],
            email: "test_" + Guid.NewGuid().ToString("N")[..8] + "@example.com",
            phoneNumber: null,
            passwordHash: passwordHash,
            nickname: "TestUser");
    }
}
