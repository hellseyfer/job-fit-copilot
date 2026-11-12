using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobFit.Api.Tests;

public sealed class UnreachableDatabaseApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:jobfit",
            "Host=127.0.0.1;Port=1;Database=jobfit;Username=jobfit;Password=unused;Timeout=2");
    }
}
