using System.Net;
using System.Text;
using System.Text.Json;
using SeedDormitoryCorridor.Online;

namespace SeedDormitoryCorridor.Online.Tests;

public sealed class OnlinePetCatalogClientTests
{
    [Fact]
    public async Task LoadsWrappedCatalog()
    {
        OnlinePetCatalogItem item = TestSupport.CreateCatalogItem([1, 2, 3]);
        string json = JsonSerializer.Serialize(new { pets = new[] { item } });
        using HttpClient httpClient = TestSupport.CreateHttpClient(_ => TestSupport.JsonResponse(json));
        var client = new OnlinePetCatalogClient(httpClient);

        IReadOnlyList<OnlinePetCatalogItem> result = await client.GetCatalogAsync(new Uri("https://catalog.example/pets"));

        OnlinePetCatalogItem loaded = Assert.Single(result);
        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal(item.Author, loaded.Author);
    }

    [Fact]
    public async Task ReportsApiErrorWithoutReturningPartialCatalog()
    {
        using HttpClient httpClient = TestSupport.CreateHttpClient(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new OnlinePetCatalogClient(httpClient);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() =>
            client.GetCatalogAsync(new Uri("https://catalog.example/pets")));

        Assert.Equal("catalog.http", exception.Code);
    }

    [Fact]
    public async Task ReportsInterruptedCatalogBodyAsNetworkError()
    {
        OnlinePetCatalogItem item = TestSupport.CreateCatalogItem([1, 2, 3]);
        byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { item }));
        using HttpClient httpClient = TestSupport.CreateHttpClient(_ =>
            TestSupport.StreamResponse(new InterruptingStream(json, json.Length / 2), json.Length));
        var client = new OnlinePetCatalogClient(httpClient);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() =>
            client.GetCatalogAsync(new Uri("https://catalog.example/pets")));

        Assert.Equal("catalog.network", exception.Code);
    }

    [Fact]
    public async Task RejectsNonHttpsCatalogUrlBeforeNetworkRequest()
    {
        int requests = 0;
        using HttpClient httpClient = TestSupport.CreateHttpClient(_ =>
        {
            requests++;
            return TestSupport.JsonResponse("[]");
        });
        var client = new OnlinePetCatalogClient(httpClient);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() =>
            client.GetCatalogAsync(new Uri("http://catalog.example/pets")));

        Assert.Equal("catalog.url", exception.Code);
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task RejectsDuplicatePetIds()
    {
        OnlinePetCatalogItem item = TestSupport.CreateCatalogItem([1, 2, 3]);
        string json = JsonSerializer.Serialize(new[] { item, item });
        using HttpClient httpClient = TestSupport.CreateHttpClient(_ => TestSupport.JsonResponse(json));
        var client = new OnlinePetCatalogClient(httpClient);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() =>
            client.GetCatalogAsync(new Uri("https://catalog.example/pets")));

        Assert.Equal("catalog.id.duplicate", exception.Code);
    }

    [Fact]
    public void DetectsIncompatibleMinimumClientVersion()
    {
        OnlinePetCatalogItem item = TestSupport.CopyCatalogItem(
            TestSupport.CreateCatalogItem([1]),
            minimumClientVersion: "0.2.0");

        Assert.False(OnlinePetCompatibility.IsCompatible(item, new Version(0, 1, 0)));
        Assert.True(OnlinePetCompatibility.IsCompatible(item, new Version(0, 2, 0)));
    }
}
