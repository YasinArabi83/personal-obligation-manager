using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using POM.Auth.Ports;

namespace POM.Api.Tests;

public sealed class CategoryFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static int _phoneSeq;

    public CategoryFlowTests(ApiFactory factory) => _factory = factory;

    private static string NextPhone() => $"+98931{Interlocked.Increment(ref _phoneSeq):D7}";

    [Fact]
    public async Task Authenticated_user_sees_defaults_and_can_create_update_delete_own_category()
    {
        var client = await AuthenticatedClientAsync();
        var list = await client.GetAsync("/api/v1/categories");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var defaults = await list.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(defaults);
        Assert.NotEmpty(defaults!);
        Assert.All(defaults!, x => Assert.True(x.GetProperty("isDefault").GetBoolean()));

        var create = await client.PostAsJsonAsync("/api/v1/categories", new { name = "  Travel  ", icon = "plane" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Travel", created.GetProperty("name").GetString());

        var update = await client.PutAsJsonAsync($"/api/v1/categories/{id}", new { name = "Trips", icon = (string?)null });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Trips", (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/categories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/categories/{id}")).StatusCode);
    }

    [Fact]
    public async Task User_cannot_modify_another_users_category_or_system_default()
    {
        var owner = await AuthenticatedClientAsync();
        var other = await AuthenticatedClientAsync();
        var create = await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Private" });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/categories/{id}", new { name = "Stolen" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/categories/{id}")).StatusCode);

        var defaults = await other.GetFromJsonAsync<JsonElement[]>("/api/v1/categories");
        var defaultId = defaults!.First(x => x.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/categories/{defaultId}", new { name = "Nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/categories/{defaultId}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_category_requests_are_rejected()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/categories")).StatusCode);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var phone = NextPhone();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone })).StatusCode);

        string code;
        using (var scope = _factory.Services.CreateScope())
            code = await scope.ServiceProvider.GetRequiredService<IOtpSender>().SendOtpAsync(phone, default);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var token = (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
