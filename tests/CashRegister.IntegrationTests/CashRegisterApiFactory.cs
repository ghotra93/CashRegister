using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CashRegister.IntegrationTests;

/// <summary>Runs the real API host in-process, in the Development environment (dev CORS origin).</summary>
public sealed class CashRegisterApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
