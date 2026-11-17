using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests;

public class HealthCheckResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class HealthCheckTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "")]
    public async Task HealthEndpoint_Returns200_AndReportsDatabaseHealthy()
    {
        var connectionString = _dbContainer.GetConnectionString();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = connectionString
                    });
                });
            });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);
        Assert.Equal("Healthy", content.Status);
        Assert.Equal("Healthy", content.Database);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task HealthEndpoint_Returns503_WhenDatabaseUnreachable()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = "Host=127.0.0.1;Port=54321;Database=nonexistent;Username=none;Password=none;Timeout=1;CommandTimeout=1;"
                    });
                });
            });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);
        Assert.Equal("Unhealthy", content.Status);
        Assert.Equal("Unhealthy", content.Database);
    }
}


