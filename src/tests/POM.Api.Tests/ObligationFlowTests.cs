using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using POM.Auth.Ports;

namespace POM.Api.Tests;

public sealed class ObligationFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static int _phoneSeq;

    public ObligationFlowTests(ApiFactory factory) => _factory = factory;

    private static string NextPhone() => $"+98931{Interlocked.Increment(ref _phoneSeq):D7}";

    [Fact]
    public async Task Authenticated_user_can_create_list_get_update_delete_and_restore_obligation()
    {
        var client = await AuthenticatedClientAsync();
        var dueDate = DateTime.UtcNow.AddDays(5);

        // Create
        var create = await client.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Task",
            title = "  Test Task  ",
            dueDate = dueDate,
            notes = "Some notes",
            priority = "High"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Test Task", created.GetProperty("title").GetString());
        Assert.Equal("Task", created.GetProperty("type").GetString());
        Assert.Equal("High", created.GetProperty("priority").GetString());
        Assert.Equal("Pending", created.GetProperty("status").GetString());

        // Get
        var get = await client.GetAsync($"/api/v1/obligations/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var got = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, got.GetProperty("id").GetGuid());

        // List
        var list = await client.GetAsync("/api/v1/obligations");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = await list.Content.ReadFromJsonAsync<JsonElement>();
        var response = JsonSerializer.Deserialize<ObligationListResponse>(items.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(response);
        Assert.Equal(1, response!.TotalCount);
        Assert.Single(response.Items);

        // Update
        var update = await client.PutAsJsonAsync($"/api/v1/obligations/{id}", new
        {
            title = "Updated Task",
            dueDate = dueDate.AddDays(2),
            priority = "Low"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Task", updated.GetProperty("title").GetString());
        Assert.Equal("Low", updated.GetProperty("priority").GetString());

        // Complete
        var complete = await client.PostAsJsonAsync($"/api/v1/obligations/{id}/complete", new { });
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);

        var getAfterComplete = await client.GetAsync($"/api/v1/obligations/{id}");
        Assert.Equal(HttpStatusCode.OK, getAfterComplete.StatusCode);
        var afterComplete = await getAfterComplete.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", afterComplete.GetProperty("status").GetString());

        // Try to update completed -> 409
        var updateAfterComplete = await client.PutAsJsonAsync($"/api/v1/obligations/{id}", new
        {
            title = "Should fail",
            dueDate = dueDate
        });
        Assert.Equal(HttpStatusCode.Conflict, updateAfterComplete.StatusCode);

        // Archive
        var archive = await client.PostAsJsonAsync($"/api/v1/obligations/{id}/archive", new { });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var getAfterArchive = await client.GetAsync($"/api/v1/obligations/{id}");
        Assert.Equal(HttpStatusCode.OK, getAfterArchive.StatusCode);
        var afterArchive = await getAfterArchive.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Archived", afterArchive.GetProperty("status").GetString());

        // Try to update archived -> 409
        var updateAfterArchive = await client.PutAsJsonAsync($"/api/v1/obligations/{id}", new
        {
            title = "Should fail",
            dueDate = dueDate
        });
        Assert.Equal(HttpStatusCode.Conflict, updateAfterArchive.StatusCode);

        // Delete (soft delete)
        var delete = await client.DeleteAsync($"/api/v1/obligations/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Should not appear in list
        var listAfterDelete = await client.GetAsync("/api/v1/obligations");
        Assert.Equal(HttpStatusCode.OK, listAfterDelete.StatusCode);
        var itemsAfterDelete = await listAfterDelete.Content.ReadFromJsonAsync<JsonElement>();
        var responseAfterDelete = JsonSerializer.Deserialize<ObligationListResponse>(itemsAfterDelete.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(0, responseAfterDelete!.TotalCount);

        // Restore
        var restore = await client.PostAsJsonAsync($"/api/v1/obligations/{id}/restore", new { });
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

        var getAfterRestore = await client.GetAsync($"/api/v1/obligations/{id}");
        Assert.Equal(HttpStatusCode.OK, getAfterRestore.StatusCode);
        var afterRestore = await getAfterRestore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Archived", afterRestore.GetProperty("status").GetString()); // status preserved
        Assert.Null(afterRestore.GetProperty("deletedAt").GetString()); // deletedAt cleared
    }

    [Fact]
    public async Task Postpone_and_skip_work()
    {
        var client = await AuthenticatedClientAsync();
        var dueDate = DateTime.UtcNow.AddDays(5);

        var create = await client.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Task",
            title = "To postpone",
            dueDate = dueDate
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        // Postpone
        var postpone = await client.PostAsJsonAsync($"/api/v1/obligations/{id}/postpone", new { newDueDate = dueDate.AddDays(10) });
        Assert.Equal(HttpStatusCode.NoContent, postpone.StatusCode);

        var getAfterPostpone = await client.GetAsync($"/api/v1/obligations/{id}");
        var afterPostpone = await getAfterPostpone.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(dueDate.AddDays(10).ToString("o").Substring(0, 16), afterPostpone.GetProperty("dueDate").GetString()!.Substring(0, 16));

        // Skip
        var skip = await client.PostAsJsonAsync($"/api/v1/obligations/{id}/skip", new { });
        Assert.Equal(HttpStatusCode.NoContent, skip.StatusCode);

        var getAfterSkip = await client.GetAsync($"/api/v1/obligations/{id}");
        var afterSkip = await getAfterSkip.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Skipped", afterSkip.GetProperty("status").GetString());

        // Try to update skipped -> 409
        var updateAfterSkip = await client.PutAsJsonAsync($"/api/v1/obligations/{id}", new { title = "Fail", dueDate = dueDate });
        Assert.Equal(HttpStatusCode.Conflict, updateAfterSkip.StatusCode);
    }

    [Fact]
    public async Task List_supports_filters_pagination_and_search()
    {
        var client = await AuthenticatedClientAsync();
        var baseDate = DateTime.UtcNow.Date.AddDays(1);

        // Create 5 obligations
        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/obligations", new
            {
                type = i % 2 == 0 ? "Task" : "Payment",
                title = $"Obligation {i}",
                dueDate = baseDate.AddDays(i),
                extraFields = i % 2 == 0 ? null : "{\"amount\": 100000}"
            });
        }

        // List with pagination
        var page1 = await client.GetAsync("/api/v1/obligations?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        var p1 = await page1.Content.ReadFromJsonAsync<JsonElement>();
        var r1 = JsonSerializer.Deserialize<ObligationListResponse>(p1.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(2, r1!.Items.Count);
        Assert.Equal(5, r1.TotalCount);
        Assert.Equal(3, r1.TotalPages);

        // Filter by type
        var tasks = await client.GetAsync("/api/v1/obligations?type=Task");
        Assert.Equal(HttpStatusCode.OK, tasks.StatusCode);
        var t = await tasks.Content.ReadFromJsonAsync<JsonElement>();
        var rt = JsonSerializer.Deserialize<ObligationListResponse>(t.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(3, rt!.TotalCount);
        foreach (var item in rt.Items)
        {
            Assert.Equal("Task", item.GetProperty("type").GetString());
        }

        // Filter by status
        var completedId = r1.Items[^1].GetProperty("id").GetGuid();
        var complete = await client.PostAsJsonAsync($"/api/v1/obligations/{completedId}/complete", new { });
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);

        var pending = await client.GetAsync("/api/v1/obligations?status=Pending");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        var p = await pending.Content.ReadFromJsonAsync<JsonElement>();
        var rp = JsonSerializer.Deserialize<ObligationListResponse>(p.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(4, rp!.TotalCount);

        // Search (ILIKE)
        var search = await client.GetAsync("/api/v1/obligations?q=obligation%201");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var s = await search.Content.ReadFromJsonAsync<JsonElement>();
        var rs = JsonSerializer.Deserialize<ObligationListResponse>(s.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(1, rs!.TotalCount);
        Assert.Contains("Obligation 1", rs.Items[0].GetProperty("title").GetString()!);

        // Order: dueDate ASC, nulls last
        var nullDue = await client.PostAsJsonAsync("/api/v1/obligations", new { type = "Task", title = "No due date", dueDate = (string?)null });
        var createdNull = await nullDue.Content.ReadFromJsonAsync<JsonElement>();
        var nullId = createdNull.GetProperty("id").GetGuid();

        var all = await client.GetAsync("/api/v1/obligations");
        var a = await all.Content.ReadFromJsonAsync<JsonElement>();
        var ra = JsonSerializer.Deserialize<ObligationListResponse>(a.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var lastItem = ra!.Items[^1];
        Assert.Equal("No due date", lastItem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task User_isolation_enforced()
    {
        var owner = await AuthenticatedClientAsync();
        var other = await AuthenticatedClientAsync();

        var create = await owner.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Task",
            title = "Owner's task",
            dueDate = DateTime.UtcNow.AddDays(5)
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        // Other user cannot get
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/obligations/{id}")).StatusCode);

        // Other user cannot update
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/obligations/{id}", new { title = "Stolen", dueDate = DateTime.UtcNow })).StatusCode);

        // Other user cannot delete
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/obligations/{id}")).StatusCode);

        // Other user cannot complete
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/obligations/{id}/complete", new { })).StatusCode);

        // Other user cannot postpone
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/obligations/{id}/postpone", new { newDueDate = DateTime.UtcNow.AddDays(1) })).StatusCode);

        // Other user cannot skip
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/obligations/{id}/skip", new { })).StatusCode);

        // Other user cannot archive
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/obligations/{id}/archive", new { })).StatusCode);

        // Other user cannot restore
        await owner.DeleteAsync($"/api/v1/obligations/{id}");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/obligations/{id}/restore", new { })).StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_rejected()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/obligations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/obligations", new { })).StatusCode);
    }

    [Fact]
    public async Task ExtraFields_validation_enforced()
    {
        var client = await AuthenticatedClientAsync();
        var dueDate = DateTime.UtcNow.AddDays(5);

        // Payment without amount -> 400
        var create = await client.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Payment",
            title = "Bad payment",
            dueDate = dueDate,
            extraFields = "{}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        // Payment with valid amount
        create = await client.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Payment",
            title = "Good payment",
            dueDate = dueDate,
            extraFields = "{\"amount\": 100000}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Good payment", created.GetProperty("title").GetString());

        // Subscription missing fields -> 400
        create = await client.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Subscription",
            title = "Bad sub",
            dueDate = dueDate,
            extraFields = "{}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        // Subscription with valid fields
        create = await client.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Subscription",
            title = "Good sub",
            dueDate = dueDate,
            extraFields = "{\"amount\": 50000, \"billingCycle\": \"Monthly\", \"nextChargeDate\": \"2026-10-01T00:00:00Z\"}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        // Document missing documentType -> 400
        create = await client.PostAsJsonAsync("/api/v1/obligations", new
        {
            type = "Document",
            title = "Bad doc",
            dueDate = dueDate,
            extraFields = "{}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
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

    private sealed class ObligationListResponse
    {
        public List<JsonElement> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}