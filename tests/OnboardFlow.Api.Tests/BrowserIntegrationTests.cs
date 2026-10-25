using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ONBOARDFLOW_BROWSER_TEST") != "1")
            Skip = "Opt-in browser integration host; run npm run verify.";
    }
}

[Trait("Category", "BrowserCI")]
public sealed class BrowserIntegrationTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [BrowserFact]
    public async Task Packaged_interface_uses_real_request_api_and_disposable_storage_in_three_browsers()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "OnboardFlow.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIntakeModelClient>();
                services.AddScoped<IIntakeModelClient>(_ => new IntakeTests.FakeModel());
            });
        });
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");
        Assert.Contains("OnboardFlow — Client onboarding", await response.Content.ReadAsStringAsync());
        var origin = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await File.WriteAllTextAsync(Path.Combine(root.FullName, ".local/reports/browser-origin.txt"), origin);
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = root.FullName, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--config=playwright.config.ts");
        start.Environment["ONBOARDFLOW_BROWSER_FIXTURE"] = "1";
        start.Environment["ONBOARDFLOW_BROWSER_ORIGIN"] = origin;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.True(process.ExitCode == 0, await stdout + "\n" + await stderr);
    }
}
