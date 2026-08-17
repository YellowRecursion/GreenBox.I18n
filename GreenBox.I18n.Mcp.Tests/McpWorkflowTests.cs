using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace GreenBox.I18n.Mcp.Tests;

public sealed class McpWorkflowTests
{
    [Fact]
    public async Task StdioServer_UsesSharedHostWorkspaceAndAppliesPreparedBatch()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        string repositoryRoot = FindRepositoryRoot();
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"greenbox-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string catalogPath = Path.Combine(temporaryDirectory, "localization.json");
        await File.WriteAllTextAsync(catalogPath, CatalogJson, timeout.Token);

        int port = GetAvailablePort();
        string hostUrl = $"http://127.0.0.1:{port}";
        string configuration = GetConfiguration();
        string hostAssembly = Path.Combine(
            repositoryRoot,
            "GreenBox.I18n.Editor.Host",
            "bin",
            configuration,
            "net10.0",
            "greenbox-i18n-host.dll");
        using Process host = StartHost(hostAssembly, hostUrl, repositoryRoot);

        try
        {
            await WaitForHostAsync(host, hostUrl, timeout.Token);
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "GreenBox I18n integration test",
                Command = "dotnet",
                Arguments = [typeof(CatalogTools).Assembly.Location],
                WorkingDirectory = repositoryRoot,
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["GREENBOX_I18N_HOST_URL"] = hostUrl,
                },
            });
            await using McpClient client = await McpClient.CreateAsync(
                transport,
                cancellationToken: timeout.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
            string[] names = tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.Contains("get_workspace", names);
            Assert.Contains("search_entries", names);
            Assert.Contains("prepare_entry_changes", names);
            Assert.Contains("apply_change_set", names);
            Assert.Contains("analyze_message", names);
            var resources = await client.ListResourcesAsync(cancellationToken: timeout.Token);
            var resourceTemplates = await client.ListResourceTemplatesAsync(cancellationToken: timeout.Token);
            var prompts = await client.ListPromptsAsync(cancellationToken: timeout.Token);
            Assert.Contains(resources, resource => resource.Name == "workspace");
            Assert.Contains(resourceTemplates, resource => resource.Name == "entry_locale_text");
            Assert.Contains(prompts, prompt => prompt.Name == "audit_catalog");
            Assert.Contains("use this server automatically", client.ServerInstructions, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("user need not mention MCP", client.ServerInstructions, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("prepare first", client.ServerInstructions, StringComparison.OrdinalIgnoreCase);

            JsonElement opened = await CallDataAsync(
                client,
                "open_catalog",
                new Dictionary<string, object?> { ["path"] = catalogPath },
                timeout.Token);
            long revision = opened.GetProperty("revision").GetInt64();

            JsonElement search = await CallDataAsync(
                client,
                "search_entries",
                new Dictionary<string, object?> { ["query"] = "Play" },
                timeout.Token);
            JsonElement found = Assert.Single(search.GetProperty("entries").EnumerateArray());
            Assert.Equal(EntryId, found.GetProperty("id").GetString());

            JsonElement issues = await CallDataAsync(
                client,
                "get_catalog_issues",
                new Dictionary<string, object?>
                {
                    ["kinds"] = new[] { "incomplete" },
                },
                timeout.Token);
            Assert.Equal(1, issues.GetProperty("totalCount").GetInt32());
            JsonElement incomplete = Assert.Single(
                issues.GetProperty("issues").EnumerateArray());
            Assert.Equal("incomplete", incomplete.GetProperty("kind").GetString());
            Assert.Equal("missing_locale_text", incomplete.GetProperty("code").GetString());
            Assert.Equal(EntryId, incomplete.GetProperty("entryId").GetString());
            Assert.Equal("ru", incomplete.GetProperty("localeId").GetString());

            JsonElement message = await CallDataAsync(
                client,
                "analyze_message",
                new Dictionary<string, object?>
                {
                    ["source"] = "Hello",
                    ["culture"] = "en-US",
                    ["arguments"] = new Dictionary<string, object?>(),
                },
                timeout.Token);
            Assert.True(
                message.GetProperty("analysis").GetProperty("isValid").GetBoolean(),
                message.ToString());
            Assert.Equal("Hello", message.GetProperty("preview").GetProperty("text").GetString());

            var changes = new[]
            {
                new
                {
                    operation = "update",
                    id = EntryId,
                    locales = new Dictionary<string, object?>
                    {
                        ["en"] = new { setText = true, text = "Start" },
                    },
                },
            };
            JsonElement prepared = await CallDataAsync(
                client,
                "prepare_entry_changes",
                new Dictionary<string, object?>
                {
                    ["expectedRevision"] = revision,
                    ["changes"] = changes,
                },
                timeout.Token);
            Assert.True(prepared.GetProperty("canApply").GetBoolean());
            string changeSetId = prepared.GetProperty("changeSetId").GetString()!;

            JsonElement applied = await CallDataAsync(
                client,
                "apply_change_set",
                new Dictionary<string, object?> { ["changeSetId"] = changeSetId },
                timeout.Token);
            Assert.True(applied.GetProperty("saved").GetBoolean());

            string saved = await File.ReadAllTextAsync(catalogPath, timeout.Token);
            Assert.Contains("\"text\": \"Start\"", saved, StringComparison.Ordinal);
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                await host.WaitForExitAsync(CancellationToken.None);
            }

            Directory.Delete(temporaryDirectory, true);
        }
    }

    private static async Task<JsonElement> CallDataAsync(
        McpClient client,
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var result = await client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);
        Assert.NotEqual(true, result.IsError);
        JsonElement structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.True(structured.GetProperty("success").GetBoolean(), structured.ToString());
        return structured.GetProperty("data");
    }

    private static Process StartHost(string assemblyPath, string hostUrl, string workingDirectory)
    {
        Assert.True(File.Exists(assemblyPath), $"Host assembly was not built: {assemblyPath}");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(hostUrl);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the Editor Host.");
    }

    private static async Task WaitForHostAsync(
        Process host,
        string hostUrl,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient { BaseAddress = new Uri(hostUrl) };
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (host.HasExited)
            {
                string standardOutput = await host.StandardOutput.ReadToEndAsync(cancellationToken);
                string standardError = await host.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Host exited with code {host.ExitCode}.\n{standardOutput}\n{standardError}");
            }

            try
            {
                using HttpResponseMessage response = await http.GetAsync("/api/session", cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Host startup is still in progress.
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string GetConfiguration()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (directory.Name is "Debug" or "Release")
            {
                return directory.Name;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not determine the test build configuration.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "GreenBox.I18n.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }

    private const string EntryId = "3857333080842834967";

    private const string CatalogJson =
        """
        {
          "schemaVersion": 1,
          "defaultLocale": "en",
          "locales": [
            {
              "id": "en",
              "displayName": "English",
              "culture": "en-US"
            },
            {
              "id": "ru",
              "displayName": "Русский",
              "culture": "ru-RU"
            }
          ],
          "entries": [
            {
              "id": "3857333080842834967",
              "path": "Menu.Play",
              "comment": "Main menu button",
              "locales": {
                "en": {
                  "text": "Play"
                }
              }
            }
          ]
        }
        """;
}
