using Leno.Infrastructure.Storage;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Infrastructure.Tests.Storage;

public class FileStorageOptionsTests
{
    [Fact]
    public void Default_Provider_ShouldBeLocal()
    {
        var options = new FileStorageOptions();

        options.Provider.Should().Be("Local");
        options.Local.Should().BeNull();
        options.ObjectStorage.Should().BeNull();
    }

    [Fact]
    public void SetProperties_ShouldStoreValues()
    {
        var options = new FileStorageOptions
        {
            Provider = "MinIO",
            Local = new LocalStorageOptions { BasePath = "/tmp", BaseUrl = "http://localhost:5000" },
            ObjectStorage = new ObjectStorageOptions
            {
                Provider = "MinIO",
                Endpoint = "localhost:9000",
                AccessKey = "minioadmin",
                SecretKey = "minioadmin",
                BucketName = "leno",
                UseSsl = false
            }
        };

        options.Provider.Should().Be("MinIO");
        options.Local.Should().NotBeNull();
        options.Local!.BasePath.Should().Be("/tmp");
        options.ObjectStorage.Should().NotBeNull();
        options.ObjectStorage!.Endpoint.Should().Be("localhost:9000");
        options.ObjectStorage.UseSsl.Should().BeFalse();
    }
}

public class ObjectStorageOptionsTests
{
    [Fact]
    public void Default_UseSsl_ShouldBeTrue()
    {
        var options = new ObjectStorageOptions();

        options.UseSsl.Should().BeTrue();
    }

    [Fact]
    public void SetProperties_ShouldStoreValues()
    {
        var options = new ObjectStorageOptions
        {
            Provider = "MinIO",
            Endpoint = "localhost:9000",
            AccessKey = "test_access_key",
            SecretKey = "test_secret_key",
            BucketName = "test-bucket",
            PublicUrl = "https://cdn.example.com",
            UseSsl = false
        };

        options.Provider.Should().Be("MinIO");
        options.Endpoint.Should().Be("localhost:9000");
        options.AccessKey.Should().Be("test_access_key");
        options.SecretKey.Should().Be("test_secret_key");
        options.BucketName.Should().Be("test-bucket");
        options.PublicUrl.Should().Be("https://cdn.example.com");
        options.UseSsl.Should().BeFalse();
    }
}

public class LocalStorageOptionsTests
{
    [Fact]
    public void Default_MaxFileSize_ShouldBe10MB()
    {
        var options = new LocalStorageOptions();

        options.MaxFileSize.Should().Be(10485760);
    }

    [Fact]
    public void Default_AllowedExtensions_ShouldBeEmpty()
    {
        var options = new LocalStorageOptions();

        options.AllowedExtensions.Should().NotBeNull();
        options.AllowedExtensions.Should().BeEmpty();
    }
}

public class LocalFileStorageServiceTests
{
    private readonly LocalFileStorageService _sut;
    private readonly LocalStorageOptions _options;

    public LocalFileStorageServiceTests()
    {
        _options = new LocalStorageOptions
        {
            BasePath = Path.Combine(Path.GetTempPath(), "leno-test", Guid.NewGuid().ToString("N")),
            BaseUrl = "http://localhost:5000/files"
        };

        var optionsWrapper = Options.Create(_options);
        var logger = new Mock<ILogger<LocalFileStorageService>>().Object;
        _sut = new LocalFileStorageService(optionsWrapper, logger);
    }

    [Fact]
    public async Task UploadAsync_Valid_ShouldReturnFileUploadResult()
    {
        var content = "Hello, Leno!"u8.ToArray();
        using var stream = new MemoryStream(content);

        var result = await _sut.UploadAsync(stream, "test.jpg", "image/jpeg", "avatar");

        result.Url.Should().StartWith("http://localhost:5000/files/avatar/");
        result.ContentType.Should().Be("image/jpeg");
        result.Size.Should().Be(content.Length);
    }

    [Fact]
    public async Task UploadAsync_InvalidCategory_ShouldUseMisc()
    {
        var content = "test"u8.ToArray();
        using var stream = new MemoryStream(content);

        var result = await _sut.UploadAsync(stream, "test.txt", "text/plain", "invalid_category");

        result.Url.Should().Contain("/misc/");
    }

    [Fact]
    public async Task UploadAsync_NullStream_ShouldThrow()
    {
        var act = () => _sut.UploadAsync(null!, "test.txt", "text/plain", "misc");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadAsync_EmptyFileName_ShouldThrow()
    {
        using var stream = new MemoryStream("test"u8.ToArray());

        var act = () => _sut.UploadAsync(stream, "", "text/plain", "misc");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*文件名*");
    }

    [Fact]
    public async Task UploadAsync_ExceedsMaxFileSize_ShouldThrow()
    {
        _options.MaxFileSize = 5;
        var optionsWrapper = Options.Create(_options);
        var logger = new Mock<ILogger<LocalFileStorageService>>().Object;
        var service = new LocalFileStorageService(optionsWrapper, logger);
        using var stream = new MemoryStream(new byte[100]);

        var act = () => service.UploadAsync(stream, "test.txt", "text/plain", "misc");

        await act.Should().ThrowAsync<Exception>().WithMessage("*文件大小*");
    }

    [Fact]
    public async Task DownloadAsync_Valid_ShouldReturnStream()
    {
        var content = "Hello, Leno!"u8.ToArray();
        using var uploadStream = new MemoryStream(content);
        var uploadResult = await _sut.UploadAsync(uploadStream, "test.txt", "text/plain", "misc");
        uploadStream.Close();

        var stream = await _sut.DownloadAsync(uploadResult.Url);
        using var reader = new StreamReader(stream);
        var downloadContent = await reader.ReadToEndAsync();

        downloadContent.Should().Be("Hello, Leno!");
    }

    [Fact]
    public async Task DownloadAsync_NotExists_ShouldThrow()
    {
        var act = () => _sut.DownloadAsync("http://localhost:5000/files/nonexistent.txt");

        await act.Should().ThrowAsync<Exception>().WithMessage("*文件不存在*");
    }

    [Fact]
    public async Task DeleteAsync_Valid_ShouldNotThrow()
    {
        var content = "test"u8.ToArray();
        using var stream = new MemoryStream(content);
        var result = await _sut.UploadAsync(stream, "test.txt", "text/plain", "misc");

        var act = () => _sut.DeleteAsync(result.Url);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_InvalidUrl_ShouldNotThrow()
    {
        var act = () => _sut.DeleteAsync("http://invalid-url/file.txt");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUrlAsync_Valid_ShouldReturnTrue()
    {
        var result = await _sut.ValidateUrlAsync("http://localhost:5000/files/test.txt");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateUrlAsync_Invalid_ShouldReturnFalse()
    {
        var result = await _sut.ValidateUrlAsync("http://invalid-url/test.txt");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateUrlAsync_Null_ShouldReturnFalse()
    {
        var result = await _sut.ValidateUrlAsync(null!);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_Uploaded_ShouldReturnTrue()
    {
        var content = "test"u8.ToArray();
        using var stream = new MemoryStream(content);
        var result = await _sut.UploadAsync(stream, "test.txt", "text/plain", "misc");

        var exists = await _sut.ExistsAsync(result.Url);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NotExists_ShouldReturnFalse()
    {
        var exists = await _sut.ExistsAsync("http://localhost:5000/files/nonexistent.txt");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_InvalidUrl_ShouldReturnFalse()
    {
        var exists = await _sut.ExistsAsync("http://invalid-url/file.txt");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetUrlAsync_Valid_ShouldReturnUrl()
    {
        var url = await _sut.GetUrlAsync("avatar/test.jpg");

        url.Should().Be("http://localhost:5000/files/avatar/test.jpg");
    }

    [Fact]
    public async Task GetUrlAsync_Null_ShouldThrow()
    {
        var act = () => _sut.GetUrlAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}