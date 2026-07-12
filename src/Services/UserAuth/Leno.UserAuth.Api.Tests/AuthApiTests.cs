using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Leno.UserAuth.Api.Tests;

public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");
        }).CreateClient();
    }

    [Fact]
    public async Task Health_Live_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}