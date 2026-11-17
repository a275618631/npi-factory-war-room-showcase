using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Xunit;

namespace UnitTests;

public class OpenApiTests
{
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null && !File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    [Trait("Category", "")]
    public void OpenApi_Specification_Contains_DemoPing_And_PingResponse()
    {
        var repoRoot = FindRepoRoot();
        var openApiJsonPath = Path.Combine(repoRoot, "src", "Web", "openapi.json");

        Assert.True(File.Exists(openApiJsonPath), $"openapi.json should exist at {openApiJsonPath}");

        var jsonContent = File.ReadAllText(openApiJsonPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths), "OpenAPI document must have 'paths'");
        Assert.True(paths.TryGetProperty("/api/demo/ping", out var pingPath), "OpenAPI document must contain '/api/demo/ping'");
        Assert.True(pingPath.TryGetProperty("get", out var getOp), "'/api/demo/ping' must have a GET operation");

        Assert.True(root.TryGetProperty("components", out var components), "OpenAPI document must have 'components'");
        Assert.True(components.TryGetProperty("schemas", out var schemas), "OpenAPI document must have 'components.schemas'");
        Assert.True(schemas.TryGetProperty("PingResponse", out var pingSchema), "components.schemas must define 'PingResponse'");

        Assert.True(pingSchema.TryGetProperty("properties", out var properties), "PingResponse must have properties");
        Assert.True(properties.TryGetProperty("message", out _), "PingResponse must have 'message' property");
        Assert.True(properties.TryGetProperty("timestamp", out _), "PingResponse must have 'timestamp' property");
    }

    [Fact]
    [Trait("Category", "")]
    public void TypeScript_Schema_Contains_PingResponse_And_Endpoint()
    {
        var repoRoot = FindRepoRoot();
        var schemaDtsPath = Path.Combine(repoRoot, "src", "Web", "src", "api", "schema.d.ts");

        Assert.True(File.Exists(schemaDtsPath), $"schema.d.ts should exist at {schemaDtsPath}");

        var schemaContent = File.ReadAllText(schemaDtsPath);

        Assert.Contains("/api/demo/ping", schemaContent);
        Assert.Contains("PingResponse", schemaContent);
        Assert.Contains("message: string", schemaContent);
        Assert.Contains("timestamp: string", schemaContent);
    }

    [Fact]
    [Trait("Category", "")]
    public void Check_Api_Types_Script_Passes_When_Synchronized()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "check-api-types.sh");

        Assert.True(File.Exists(scriptPath), $"check-api-types.sh should exist at {scriptPath}");

        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = scriptPath,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi);
        Assert.NotNull(proc);
        proc.WaitForExit();

        var output = proc.StandardOutput.ReadToEnd();
        var error = proc.StandardError.ReadToEnd();

        Assert.True(proc.ExitCode == 0, $"check-api-types.sh failed with code {proc.ExitCode}. Out: {output}. Err: {error}");
    }

    [Fact]
    [Trait("Category", "")]
    public void Check_Api_Types_Script_Fails_When_Backend_Api_Changes_Without_Regeneration()
    {
        var repoRoot = FindRepoRoot();
        var programCsPath = Path.Combine(repoRoot, "src", "Platform.Host", "Program.cs");
        var openApiJsonPath = Path.Combine(repoRoot, "src", "Web", "openapi.json");
        var schemaDtsPath = Path.Combine(repoRoot, "src", "Web", "src", "api", "schema.d.ts");

        var originalProgramCs = File.ReadAllText(programCsPath);
        var originalOpenApiJson = File.ReadAllText(openApiJsonPath);
        var originalSchemaDts = File.ReadAllText(schemaDtsPath);

        try
        {
            // Simulate backend modification: add an optional property to PingResponse so backend still builds
            var modifiedContent = originalProgramCs.Replace(
                "public record PingResponse(string Message, DateTime Timestamp);",
                "public record PingResponse(string Message, DateTime Timestamp, string? Note = null);"
            );
            File.WriteAllText(programCsPath, modifiedContent);

            var scriptPath = Path.Combine(repoRoot, "scripts", "check-api-types.sh");
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = scriptPath,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(psi);
            Assert.NotNull(proc);
            proc.WaitForExit();

            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            var combined = output + "\n" + error;
            Assert.NotEqual(0, proc.ExitCode);
            Assert.Contains("=== Diff detected ===", combined);
        }
        finally
        {
            File.WriteAllText(programCsPath, originalProgramCs);
            File.WriteAllText(openApiJsonPath, originalOpenApiJson);
            File.WriteAllText(schemaDtsPath, originalSchemaDts);
        }
    }
}


