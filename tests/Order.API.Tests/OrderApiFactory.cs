using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.API.Clients;

namespace Order.API.Tests;

public sealed class OrderApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"order-tests-{Guid.NewGuid():N}.db");

    public StubCatalogHandler Catalog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderDatabase"] = $"Data Source={_databasePath};Pooling=False",
                ["Services:Catalog"] = "http://catalog.test"
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Catalog);
            services.AddHttpClient<CatalogClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<StubCatalogHandler>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            DeleteDatabaseFile(_databasePath);
            DeleteDatabaseFile($"{_databasePath}-shm");
            DeleteDatabaseFile($"{_databasePath}-wal");
        }
    }

    private static void DeleteDatabaseFile(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(25);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(25);
            }
        }
    }
}
