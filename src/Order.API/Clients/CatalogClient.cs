using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Order.API.Clients;

public sealed class CatalogClient(HttpClient httpClient)
{
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync("/health/ready", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CatalogUnavailableException("Catalog API health request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new CatalogUnavailableException("Catalog API health request failed.", exception);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new CatalogUnavailableException("Catalog API health request failed.", exception);
        }
    }

    public async Task<CatalogProduct?> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"/api/products/{productId}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<CatalogProduct>(
                    cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("Catalog API returned an empty response.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CatalogUnavailableException("Catalog API request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new CatalogUnavailableException("Catalog API request failed.", exception);
        }
        catch (JsonException exception)
        {
            throw new CatalogUnavailableException("Catalog API returned an invalid response.", exception);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new CatalogUnavailableException("Catalog API request failed.", exception);
        }
    }
}

public sealed record CatalogProduct(Guid Id, string Name, decimal Price, int Stock);

public sealed class CatalogUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
