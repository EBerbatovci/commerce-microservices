using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Catalog.API.Tests;

public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"catalog-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogDatabase"] = $"Data Source={_databasePath};Pooling=False"
            }));
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
