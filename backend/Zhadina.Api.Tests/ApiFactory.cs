using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Zhadina.Api.Data;

namespace Zhadina.Api.Tests;

// Each factory gets its own throwaway database on the local server, dropped on dispose.
// Point ZHADINA_TEST_PG at another server if yours isn't on localhost:5432 with trust auth.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString =
        $"{Environment.GetEnvironmentVariable("ZHADINA_TEST_PG") ?? "Host=localhost;Port=5432;Username=postgres"};" +
        $"Database=zhadina_test_{Guid.NewGuid():N}";

    protected virtual int AuthPermitLimit => 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // DATABASE_URL wins over ConnectionStrings:Default, so override it to never touch a real database.
        builder.UseSetting("DATABASE_URL", _connectionString);
        builder.UseSetting("RateLimiting:AuthPermitLimit", AuthPermitLimit.ToString());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        await DisposeAsync();
    }
}

public class RateLimitedApiFactory : ApiFactory
{
    protected override int AuthPermitLimit => 2;
}
