using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using NUnit.Framework;
using Z9Flex;
using Z9Flex.Client;
using Z9Flex.Client.Models;
using Enums = Z9Flex.Enums;

namespace Z9Flex.LoopbackTest;

[TestFixture]
public class LoopbackTest
{
    private WebApplication _app = null!;
    private string _baseUrl = null!;
    private FlexClient _client = null!;
    private FlexClient _unauthClient = null!;
    private Z9AuthenticationProvider _authProvider = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(k => k.Listen(System.Net.IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();

        _app = builder.Build();

        string? validSessionToken = null;

        _app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/authenticate"))
            {
                string? token = context.Request.Headers["sessionToken"];
                if (token == null || token != validSessionToken)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("{}");
                    return;
                }
            }
            await next();
        });

        _app.MapPost("/authenticate", async (HttpContext ctx) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonObject>(ctx.Request.Body);
            var username = body?["username"]?.GetValue<string>();
            var password = body?["password"]?.GetValue<string>();

            if (username == "admin" && password == "admin123")
            {
                validSessionToken = Guid.NewGuid().ToString();
                return Results.Json(new
                {
                    authenticated = true,
                    sessionToken = validSessionToken,
                    apiVersion = "1.0",
                    softwareVersion = "test",
                    timeZone = "UTC"
                });
            }
            return Results.Json(new { authenticated = false });
        });

        _app.MapGet("/terminate", () =>
        {
            validSessionToken = null;
            return Results.Json(new object());
        });

        // --- CRUD resource registration ---
        var stores = new Dictionary<string, Dictionary<int, JsonObject>>();
        var counters = new Dictionary<string, int>();

        void RegisterResource(string name)
        {
            stores[name] = new Dictionary<int, JsonObject>();
            counters[name] = 1;

            _app.MapGet($"/{name}/list", (HttpContext ctx) =>
            {
                var store = stores[name];
                int.TryParse(ctx.Request.Query["offset"], out var offset);
                int? max = int.TryParse(ctx.Request.Query["max"], out var m) ? m : null;
                var order = (string?)ctx.Request.Query["order"];

                var items = store.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
                if (string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase))
                    items.Reverse();

                var totalCount = items.Count;
                var page = items.Skip(offset);
                if (max.HasValue) page = page.Take(max.Value);

                var result = new JsonObject
                {
                    ["count"] = totalCount,
                    ["offset"] = offset,
                    ["max"] = max,
                    ["instanceList"] = new JsonArray(page.Select(i => i.DeepClone()).ToArray())
                };
                return Results.Content(result.ToJsonString(), "application/json");
            });

            _app.MapGet($"/{name}/show/{{id}}", (string id) =>
            {
                var store = stores[name];
                if (!store.TryGetValue(int.Parse(id), out var obj))
                    return Results.NotFound();
                var result = new JsonObject { ["instance"] = obj.DeepClone() };
                return Results.Content(result.ToJsonString(), "application/json");
            });

            _app.MapPost($"/{name}/save", async (HttpContext ctx) =>
            {
                var body = await JsonSerializer.DeserializeAsync<JsonObject>(ctx.Request.Body);
                var id = counters[name]++;
                body!["unid"] = id;
                if (body["uuid"] == null) body["uuid"] = Guid.NewGuid().ToString();
                stores[name][id] = body;
                var result = new JsonObject { ["instance"] = body.DeepClone() };
                return Results.Content(result.ToJsonString(), "application/json");
            });

            _app.MapPost($"/{name}/update/{{id}}", async (HttpContext ctx, string id) =>
            {
                var body = await JsonSerializer.DeserializeAsync<JsonObject>(ctx.Request.Body);
                var upsert = string.Equals(ctx.Request.Query["upsert"], "true", StringComparison.OrdinalIgnoreCase);

                // Try numeric (unid) first, then UUID lookup
                if (int.TryParse(id, out var numericId) && stores[name].ContainsKey(numericId))
                {
                    body!["unid"] = numericId;
                    body["uuid"] = stores[name][numericId]["uuid"]?.DeepClone();
                    stores[name][numericId] = body;
                }
                else
                {
                    // UUID lookup
                    var existing = stores[name].FirstOrDefault(kv =>
                        kv.Value["uuid"]?.GetValue<string>() == id);

                    if (existing.Value != null)
                    {
                        body!["unid"] = existing.Key;
                        body["uuid"] = id;
                        stores[name][existing.Key] = body;
                    }
                    else if (upsert)
                    {
                        // Upsert: create new entry with this UUID
                        var newId = counters[name]++;
                        body!["unid"] = newId;
                        body["uuid"] = id;
                        stores[name][newId] = body;
                    }
                    else
                    {
                        return Results.NotFound();
                    }
                }

                var result = new JsonObject { ["instance"] = body!.DeepClone() };
                return Results.Content(result.ToJsonString(), "application/json");
            });

            _app.MapPost($"/{name}/delete/{{id}}", (string id) =>
            {
                if (int.TryParse(id, out var numericId))
                {
                    stores[name].Remove(numericId);
                }
                else
                {
                    var existing = stores[name].FirstOrDefault(kv =>
                        kv.Value["uuid"]?.GetValue<string>() == id);
                    if (existing.Value != null)
                        stores[name].Remove(existing.Key);
                }
                return Results.Content("{}", "application/json");
            });
        }

        foreach (var resource in new[]
        {
            "sched", "hol", "holType", "holCal", "credTemplate", "dataFormat",
            "dataLayout", "cred", "doorAccessPriv", "encryptionKey", "door",
            "dev", "sensor", "actuator", "credReader", "controller", "nodeDev",
            "basicDataLayout", "binaryFormat"
        })
        {
            RegisterResource(resource);
        }

        // Read-only endpoints — pre-populated with fake data
        var evtStore = new Dictionary<int, JsonObject>
        {
            [1] = new JsonObject
            {
                ["unid"] = 1, ["uuid"] = Guid.NewGuid().ToString(),
                ["evtCode"] = 4, ["priority"] = 1, ["data"] = "door-opened"
            },
            [2] = new JsonObject
            {
                ["unid"] = 2, ["uuid"] = Guid.NewGuid().ToString(),
                ["evtCode"] = 7, ["priority"] = 2, ["data"] = "access-granted"
            }
        };

        _app.MapGet("/evt/list", (HttpContext ctx) =>
        {
            int.TryParse(ctx.Request.Query["offset"], out var offset);
            int? max = int.TryParse(ctx.Request.Query["max"], out var m) ? m : null;
            var items = evtStore.Values.ToList();
            var page = items.Skip(offset);
            if (max.HasValue) page = page.Take(max.Value);
            return Results.Content(new JsonObject
            {
                ["count"] = items.Count, ["offset"] = offset, ["max"] = max,
                ["instanceList"] = new JsonArray(page.Select(i => i.DeepClone()).ToArray())
            }.ToJsonString(), "application/json");
        });

        _app.MapGet("/evt/show/{id}", (string id) =>
        {
            if (!int.TryParse(id, out var numId) || !evtStore.TryGetValue(numId, out var evt))
                return Results.NotFound();
            return Results.Content(new JsonObject { ["instance"] = evt.DeepClone() }.ToJsonString(), "application/json");
        });

        var dsrStore = new Dictionary<int, JsonObject>
        {
            [1] = new JsonObject
            {
                ["unid"] = 1,
                ["dev"] = new JsonObject { ["unid"] = 10, ["name"] = "Front Door" },
                ["devState"] = new JsonObject { ["doorMode"] = "LOCKED" }
            }
        };

        _app.MapGet("/devStateRecord/list", (HttpContext ctx) =>
        {
            int.TryParse(ctx.Request.Query["offset"], out var offset);
            int? max = int.TryParse(ctx.Request.Query["max"], out var m) ? m : null;
            var items = dsrStore.Values.ToList();
            var page = items.Skip(offset);
            if (max.HasValue) page = page.Take(max.Value);
            return Results.Content(new JsonObject
            {
                ["count"] = items.Count, ["offset"] = offset, ["max"] = max,
                ["instanceList"] = new JsonArray(page.Select(i => i.DeepClone()).ToArray())
            }.ToJsonString(), "application/json");
        });

        // JSON operational endpoints (non-CRUD) — resolve door by unid, uuid, or tag
        IResult ResolveDoorEndpoint(HttpContext ctx)
        {
            var doorStore = stores["door"];
            int.TryParse(ctx.Request.Query["unid"], out var unid);
            var uuid = (string?)ctx.Request.Query["uuid"];
            var tag = (string?)ctx.Request.Query["tag"];

            var found = doorStore.Values.FirstOrDefault(d =>
                (unid > 0 && d["unid"]?.GetValue<int>() == unid) ||
                (uuid != null && d["uuid"]?.GetValue<string>() == uuid) ||
                (tag != null && d["tag"]?.GetValue<string>() == tag));

            if (found == null) return Results.NotFound();
            return Results.Content("{}", "application/json");
        }
        _app.MapGet("/json/doorModeChange", (HttpContext ctx) => ResolveDoorEndpoint(ctx));
        _app.MapGet("/json/doorMomentaryUnlock", (HttpContext ctx) => ResolveDoorEndpoint(ctx));

        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        _baseUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();

        var unauthAdapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(),
            httpClient: new HttpClient()) { BaseUrl = _baseUrl };
        _unauthClient = new FlexClient(unauthAdapter);

        _authProvider = Z9AuthenticationProvider.CreateInstance(
            _baseUrl, () => ("admin", "admin123"), new HttpClientHandler());
        var authAdapter = new HttpClientRequestAdapter(
            _authProvider,
            httpClient: new Z9HttpClient(new HttpClientHandler(), _authProvider))
        { BaseUrl = _baseUrl };
        _client = new FlexClient(authAdapter);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task FullCrudWorkflow()
    {
        // --- Authentication ---

        // 401 without token
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _unauthClient.Sched.List.GetAsync());
        Assert.That(ex!.ResponseStatusCode, Is.EqualTo(401));

        // Good credentials
        var authResult = await _unauthClient.Authenticate.PostAsync(
            new AuthenticateRequest { Username = "admin", Password = "admin123", ApiClientType = 2 });
        Assert.That(authResult!.Authenticated, Is.True);
        Assert.That(authResult.SessionToken, Is.Not.Null.And.Not.Empty);

        // Bad credentials
        var badResult = await _unauthClient.Authenticate.PostAsync(
            new AuthenticateRequest { Username = "admin", Password = "wrong", ApiClientType = 2 });
        Assert.That(badResult!.Authenticated, Is.False);

        // --- CRUD: simple types ---

        await CrudSched();
        await CrudHolType();
        await CrudHolCal();
        await CrudHol();
        await CrudCredTemplate();
        await CrudCred();
        await CrudEncryptionKey();

        // --- CRUD: polymorphic base-class endpoints ---

        await CrudDoorAccessPriv();
        await CrudDataFormat();
        await CrudBinaryFormat();
        await CrudDataLayout();
        await CrudBasicDataLayout();

        // --- CRUD: Dev hierarchy ---

        await CrudDoor();
        await CrudSensor();
        await CrudActuator();
        await CrudCredReader();
        await CrudController();
        await CrudNodeDev();
        await CrudDev();

        // --- Read-only endpoints ---

        // Evt list
        var evtList = await _client.Evt.List.GetAsync();
        Assert.That(evtList!.Count, Is.EqualTo(2));
        Assert.That(evtList.InstanceList, Has.Count.EqualTo(2));
        Assert.That(evtList.InstanceList![0].EvtCode, Is.EqualTo(4));
        Assert.That(evtList.InstanceList[0].Data, Is.EqualTo("door-opened"));
        Assert.That(evtList.InstanceList[1].EvtCode, Is.EqualTo(7));

        // Evt list with pagination
        var evtPage = await _client.Evt.List.GetAsync(cfg => { cfg.QueryParameters.Max = 1; });
        Assert.That(evtPage!.Count, Is.EqualTo(2));
        Assert.That(evtPage.InstanceList, Has.Count.EqualTo(1));

        // Evt show
        var evtShow = await _client.Evt.Show["1"].GetAsShowGetResponseAsync();
        Assert.That(evtShow!.Instance!.Unid, Is.EqualTo(1));
        Assert.That(evtShow.Instance.Data, Is.EqualTo("door-opened"));

        // Evt show — not found
        var evtNotFound = Assert.ThrowsAsync<ApiException>(async () =>
            await _client.Evt.Show["999"].GetAsShowGetResponseAsync());
        Assert.That(evtNotFound!.ResponseStatusCode, Is.EqualTo(404));

        // DevStateRecord list
        var dsrList = await _client.DevStateRecord.List.GetAsync();
        Assert.That(dsrList!.Count, Is.EqualTo(1));
        Assert.That(dsrList.InstanceList, Has.Count.EqualTo(1));
        Assert.That(dsrList.InstanceList![0].Unid, Is.EqualTo(1));

        // --- Pagination ---

        await _client.Sched.Save.PostAsSavePostResponseAsync(new Sched { Name = "P1" });
        await _client.Sched.Save.PostAsSavePostResponseAsync(new Sched { Name = "P2" });
        await _client.Sched.Save.PostAsSavePostResponseAsync(new Sched { Name = "P3" });

        var paginated = await _client.Sched.List.GetAsync(cfg => { cfg.QueryParameters.Max = 2; });
        Assert.That(paginated!.Count, Is.EqualTo(3));
        Assert.That(paginated.InstanceList, Has.Count.EqualTo(2));

        // --- JSON operational endpoints ---

        await TestJsonOperationalEndpoints();

        // --- Order ---

        await TestOrderSched();

        // --- Upsert ---

        await TestUpsertSched();

        // --- Terminate ---

        var oldToken = _authProvider.CurrentAuthenticationResult?.SessionToken;
        Assert.That(oldToken, Is.Not.Null.And.Not.Empty);

        await _client.Terminate.GetAsync();

        using var rawClient = new HttpClient();
        rawClient.DefaultRequestHeaders.Add("sessionToken", oldToken);
        var response = await rawClient.GetAsync($"{_baseUrl}/sched/list");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // --- Simple types ---

    private async Task CrudSched()
    {
        var save = await _client.Sched.Save.PostAsSavePostResponseAsync(
            new Sched { Name = "Test Sched", Tag = "t" });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Sched"));

        var list = await _client.Sched.List.GetAsync();
        Assert.That(list!.Count, Is.EqualTo(1));

        var upd = await _client.Sched.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(new Sched { Name = "Updated Sched" });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Sched"));
        Assert.That(upd.Instance.Unid, Is.EqualTo(unid));

        var show = await _client.Sched.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Sched"));
        Assert.That(show.Instance.Unid, Is.EqualTo(unid));

        await _client.Sched.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Sched.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudHolType()
    {
        var save = await _client.HolType.Save.PostAsSavePostResponseAsync(
            new HolType { Name = "Test HolType" });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test HolType"));

        Assert.That((await _client.HolType.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.HolType.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(new HolType { Name = "Updated HolType" });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated HolType"));

        var show = await _client.HolType.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated HolType"));

        await _client.HolType.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.HolType.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudHolCal()
    {
        var save = await _client.HolCal.Save.PostAsSavePostResponseAsync(
            new HolCal { Name = "Test HolCal" });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test HolCal"));

        Assert.That((await _client.HolCal.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.HolCal.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(new HolCal { Name = "Updated HolCal" });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated HolCal"));

        var show = await _client.HolCal.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated HolCal"));

        await _client.HolCal.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.HolCal.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudHol()
    {
        var save = await _client.Hol.Save.PostAsSavePostResponseAsync(
            new Hol { Name = "Test Holiday", NumDays = 1, Repeat = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Holiday"));

        Assert.That((await _client.Hol.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.Hol.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(new Hol { Name = "Updated Holiday" });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Holiday"));

        var show = await _client.Hol.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Holiday"));

        await _client.Hol.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Hol.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudCredTemplate()
    {
        var save = await _client.CredTemplate.Save.PostAsSavePostResponseAsync(
            new CredTemplate { Name = "Test CredTemplate" });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test CredTemplate"));

        Assert.That((await _client.CredTemplate.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.CredTemplate.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(new CredTemplate { Name = "Updated CredTemplate" });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated CredTemplate"));

        var show = await _client.CredTemplate.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated CredTemplate"));

        await _client.CredTemplate.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.CredTemplate.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudCred()
    {
        var save = await _client.Cred.Save.PostAsSavePostResponseAsync(
            new Cred { Name = "Test Cred", Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Cred"));

        Assert.That((await _client.Cred.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.Cred.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(new Cred { Name = "Updated Cred", Enabled = false });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Cred"));

        var show = await _client.Cred.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Cred"));

        await _client.Cred.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Cred.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudEncryptionKey()
    {
        var save = await _client.EncryptionKey.Save.PostAsSavePostResponseAsync(
            new EncryptionKey { KeyIdentifier = "test-key", Algorithm = "AES", Size = 256 });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.KeyIdentifier, Is.EqualTo("test-key"));

        Assert.That((await _client.EncryptionKey.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.EncryptionKey.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(new EncryptionKey { KeyIdentifier = "updated-key", Algorithm = "RSA" });
        Assert.That(upd!.Instance!.KeyIdentifier, Is.EqualTo("updated-key"));

        var show = await _client.EncryptionKey.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.KeyIdentifier, Is.EqualTo("updated-key"));

        await _client.EncryptionKey.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.EncryptionKey.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    // --- Polymorphic types ---

    private async Task CrudDoorAccessPriv()
    {
        var save = await _client.DoorAccessPriv.Save.PostAsSavePostResponseAsync(
            new DoorAccessPriv { Name = "Test DoorAccessPriv", PrivType = Enums.PrivType.Door });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test DoorAccessPriv"));

        Assert.That((await _client.DoorAccessPriv.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.DoorAccessPriv.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new DoorAccessPriv { Name = "Updated DoorAccessPriv", PrivType = Enums.PrivType.Door });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated DoorAccessPriv"));

        var show = await _client.DoorAccessPriv.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated DoorAccessPriv"));

        await _client.DoorAccessPriv.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.DoorAccessPriv.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudDataFormat()
    {
        var save = await _client.DataFormat.Save.PostAsSavePostResponseAsync(
            new BinaryFormat { Name = "Test DataFormat", DataFormatType = Enums.DataFormatType.Binary,
                MinBits = 26, MaxBits = 37 });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test DataFormat"));

        Assert.That((await _client.DataFormat.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.DataFormat.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new BinaryFormat { Name = "Updated DataFormat", DataFormatType = Enums.DataFormatType.Binary });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated DataFormat"));

        var show = await _client.DataFormat.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated DataFormat"));

        await _client.DataFormat.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.DataFormat.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudBinaryFormat()
    {
        var save = await _client.BinaryFormat.Save.PostAsSavePostResponseAsync(
            new BinaryFormat { Name = "Test BinaryFormat", DataFormatType = Enums.DataFormatType.Binary,
                MinBits = 26, MaxBits = 34 });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test BinaryFormat"));

        Assert.That((await _client.BinaryFormat.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.BinaryFormat.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new BinaryFormat { Name = "Updated BinaryFormat", DataFormatType = Enums.DataFormatType.Binary });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated BinaryFormat"));

        var show = await _client.BinaryFormat.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated BinaryFormat"));

        await _client.BinaryFormat.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.BinaryFormat.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudDataLayout()
    {
        var save = await _client.DataLayout.Save.PostAsSavePostResponseAsync(
            new BasicDataLayout { Name = "Test DataLayout", LayoutType = Enums.DataLayoutType.Basic,
                Enabled = true, Priority = 1 });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test DataLayout"));

        Assert.That((await _client.DataLayout.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.DataLayout.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new BasicDataLayout { Name = "Updated DataLayout", LayoutType = Enums.DataLayoutType.Basic });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated DataLayout"));

        var show = await _client.DataLayout.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated DataLayout"));

        await _client.DataLayout.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.DataLayout.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudBasicDataLayout()
    {
        var save = await _client.BasicDataLayout.Save.PostAsSavePostResponseAsync(
            new BasicDataLayout { Name = "Test BasicDataLayout", LayoutType = Enums.DataLayoutType.Basic,
                Enabled = true, Priority = 1 });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test BasicDataLayout"));

        Assert.That((await _client.BasicDataLayout.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.BasicDataLayout.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new BasicDataLayout { Name = "Updated BasicDataLayout", LayoutType = Enums.DataLayoutType.Basic });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated BasicDataLayout"));

        var show = await _client.BasicDataLayout.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated BasicDataLayout"));

        await _client.BasicDataLayout.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.BasicDataLayout.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    // --- Dev hierarchy ---

    private async Task CrudDoor()
    {
        var save = await _client.Door.Save.PostAsSavePostResponseAsync(
            new Door { Name = "Test Door", DevType = Enums.DevType.Door,
                DevMod = Enums.DevMod.DoorCommunity, DevPlatform = Enums.DevPlatform.Community,
                Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Door"));
        Assert.That(save.Instance.DevType, Is.EqualTo(Enums.DevType.Door));

        Assert.That((await _client.Door.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.Door.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new Door { Name = "Updated Door", DevType = Enums.DevType.Door,
                    DevMod = Enums.DevMod.DoorCommunity, DevPlatform = Enums.DevPlatform.Community });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Door"));

        var show = await _client.Door.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Door"));

        await _client.Door.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Door.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudSensor()
    {
        var save = await _client.Sensor.Save.PostAsSavePostResponseAsync(
            new Sensor { Name = "Test Sensor", DevType = Enums.DevType.Sensor,
                DevMod = Enums.DevMod.SensorDigital, DevPlatform = Enums.DevPlatform.Community,
                Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Sensor"));
        Assert.That(save.Instance.DevType, Is.EqualTo(Enums.DevType.Sensor));

        Assert.That((await _client.Sensor.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.Sensor.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new Sensor { Name = "Updated Sensor", DevType = Enums.DevType.Sensor,
                    DevMod = Enums.DevMod.SensorDigital });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Sensor"));

        var show = await _client.Sensor.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Sensor"));

        await _client.Sensor.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Sensor.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudActuator()
    {
        var save = await _client.Actuator.Save.PostAsSavePostResponseAsync(
            new Actuator { Name = "Test Actuator", DevType = Enums.DevType.Actuator,
                DevMod = Enums.DevMod.ActuatorDigital, DevPlatform = Enums.DevPlatform.Community,
                Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Actuator"));
        Assert.That(save.Instance.DevType, Is.EqualTo(Enums.DevType.Actuator));

        Assert.That((await _client.Actuator.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.Actuator.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new Actuator { Name = "Updated Actuator", DevType = Enums.DevType.Actuator,
                    DevMod = Enums.DevMod.ActuatorDigital });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Actuator"));

        var show = await _client.Actuator.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Actuator"));

        await _client.Actuator.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Actuator.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudCredReader()
    {
        var save = await _client.CredReader.Save.PostAsSavePostResponseAsync(
            new CredReader { Name = "Test CredReader", DevType = Enums.DevType.CredReader,
                DevMod = Enums.DevMod.CredReaderOsdp, DevPlatform = Enums.DevPlatform.Community,
                Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test CredReader"));
        Assert.That(save.Instance.DevType, Is.EqualTo(Enums.DevType.CredReader));

        Assert.That((await _client.CredReader.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.CredReader.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new CredReader { Name = "Updated CredReader", DevType = Enums.DevType.CredReader,
                    DevMod = Enums.DevMod.CredReaderOsdp });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated CredReader"));

        var show = await _client.CredReader.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated CredReader"));

        await _client.CredReader.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.CredReader.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudController()
    {
        var save = await _client.Controller.Save.PostAsSavePostResponseAsync(
            new Controller { Name = "Test Controller", DevType = Enums.DevType.IoController,
                DevMod = Enums.DevMod.IoControllerCommunity, DevPlatform = Enums.DevPlatform.Community,
                Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Controller"));
        Assert.That(save.Instance.DevType, Is.EqualTo(Enums.DevType.IoController));

        Assert.That((await _client.Controller.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.Controller.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new Controller { Name = "Updated Controller", DevType = Enums.DevType.IoController,
                    DevMod = Enums.DevMod.IoControllerCommunity });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Controller"));

        var show = await _client.Controller.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Controller"));

        await _client.Controller.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Controller.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudNodeDev()
    {
        var save = await _client.NodeDev.Save.PostAsSavePostResponseAsync(
            new NodeDev { Name = "Test NodeDev", DevType = Enums.DevType.NodeDev,
                DevPlatform = Enums.DevPlatform.Community, Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test NodeDev"));
        Assert.That(save.Instance.DevType, Is.EqualTo(Enums.DevType.NodeDev));

        Assert.That((await _client.NodeDev.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.NodeDev.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new NodeDev { Name = "Updated NodeDev", DevType = Enums.DevType.NodeDev });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated NodeDev"));

        var show = await _client.NodeDev.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated NodeDev"));

        await _client.NodeDev.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.NodeDev.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    private async Task CrudDev()
    {
        // Use /dev/save with a concrete subclass (Controller)
        var save = await _client.Dev.Save.PostAsSavePostResponseAsync(
            new Controller { Name = "Test Dev", DevType = Enums.DevType.IoController,
                DevMod = Enums.DevMod.IoControllerCommunity, DevPlatform = Enums.DevPlatform.Community,
                Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        Assert.That(save.Instance.Name, Is.EqualTo("Test Dev"));
        Assert.That(save.Instance.DevType, Is.EqualTo(Enums.DevType.IoController));

        Assert.That((await _client.Dev.List.GetAsync())!.Count, Is.EqualTo(1));

        var upd = await _client.Dev.Update[unid.ToString()]
            .PostAsUpdatePostResponseAsync(
                new Controller { Name = "Updated Dev", DevType = Enums.DevType.IoController,
                    DevMod = Enums.DevMod.IoControllerCommunity });
        Assert.That(upd!.Instance!.Name, Is.EqualTo("Updated Dev"));

        var show = await _client.Dev.Show[unid.ToString()].GetAsShowGetResponseAsync();
        Assert.That(show!.Instance!.Name, Is.EqualTo("Updated Dev"));

        await _client.Dev.DeletePath[unid.ToString()].PostAsync();
        Assert.That((await _client.Dev.List.GetAsync())!.Count, Is.EqualTo(0));
    }

    // --- JSON operational endpoints ---

    private async Task TestJsonOperationalEndpoints()
    {
        // Save a door to target
        var save = await _client.Door.Save.PostAsSavePostResponseAsync(
            new Door { Name = "OpsDoor", Tag = "ops-door-1",
                DevType = Enums.DevType.Door, DevMod = Enums.DevMod.DoorCommunity,
                DevPlatform = Enums.DevPlatform.Community, Enabled = true });
        var unid = save!.Instance!.Unid!.Value;
        var uuid = save.Instance.Uuid;

        // DoorModeChange by unid — should succeed (no exception)
        await _client.Json.DoorModeChange.GetAsync(cfg =>
        {
            cfg.QueryParameters.Unid = unid;
            cfg.QueryParameters.Value = "UNLOCK";
        });

        // DoorMomentaryUnlock by tag — should succeed
        await _client.Json.DoorMomentaryUnlock.GetAsync(cfg =>
        {
            cfg.QueryParameters.Tag = "ops-door-1";
            cfg.QueryParameters.StrikeTime = 5;
        });

        // DoorModeChange by uuid — should succeed
        await _client.Json.DoorModeChange.GetAsync(cfg =>
        {
            cfg.QueryParameters.Uuid = uuid;
            cfg.QueryParameters.Value = "RESET";
        });

        // Unknown device — should get 404
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _client.Json.DoorModeChange.GetAsync(cfg =>
            {
                cfg.QueryParameters.Unid = 99999;
                cfg.QueryParameters.Value = "UNLOCK";
            }));
        Assert.That(ex!.ResponseStatusCode, Is.EqualTo(404));

        // Cleanup
        await _client.Door.DeletePath[unid.ToString()].PostAsync();
    }

    // --- Order ---

    private async Task TestOrderSched()
    {
        // Clean slate — sched store has leftover pagination items, clear them
        var existing = await _client.Sched.List.GetAsync();
        foreach (var item in existing!.InstanceList!)
            await _client.Sched.DeletePath[item.Unid!.Value.ToString()].PostAsync();

        // Save 3 items — they get ascending unids
        var s1 = (await _client.Sched.Save.PostAsSavePostResponseAsync(new Sched { Name = "Alpha" }))!.Instance!;
        var s2 = (await _client.Sched.Save.PostAsSavePostResponseAsync(new Sched { Name = "Bravo" }))!.Instance!;
        var s3 = (await _client.Sched.Save.PostAsSavePostResponseAsync(new Sched { Name = "Charlie" }))!.Instance!;

        // List ascending (default / explicit)
        var asc = await _client.Sched.List.GetAsync(cfg =>
        {
            cfg.QueryParameters.OrderAsGetOrderQueryParameterType =
                Z9Flex.Client.Sched.List.GetOrderQueryParameterType.Asc;
        });
        Assert.That(asc!.InstanceList, Has.Count.EqualTo(3));
        Assert.That(asc.InstanceList![0].Name, Is.EqualTo("Alpha"));
        Assert.That(asc.InstanceList[2].Name, Is.EqualTo("Charlie"));

        // List descending
        var desc = await _client.Sched.List.GetAsync(cfg =>
        {
            cfg.QueryParameters.OrderAsGetOrderQueryParameterType =
                Z9Flex.Client.Sched.List.GetOrderQueryParameterType.Desc;
        });
        Assert.That(desc!.InstanceList, Has.Count.EqualTo(3));
        Assert.That(desc.InstanceList![0].Name, Is.EqualTo("Charlie"));
        Assert.That(desc.InstanceList[2].Name, Is.EqualTo("Alpha"));

        // Descending + pagination
        var descPage = await _client.Sched.List.GetAsync(cfg =>
        {
            cfg.QueryParameters.OrderAsGetOrderQueryParameterType =
                Z9Flex.Client.Sched.List.GetOrderQueryParameterType.Desc;
            cfg.QueryParameters.Max = 2;
        });
        Assert.That(descPage!.Count, Is.EqualTo(3));
        Assert.That(descPage.InstanceList, Has.Count.EqualTo(2));
        Assert.That(descPage.InstanceList![0].Name, Is.EqualTo("Charlie"));
        Assert.That(descPage.InstanceList[1].Name, Is.EqualTo("Bravo"));

        // Cleanup
        await _client.Sched.DeletePath[s1.Unid!.Value.ToString()].PostAsync();
        await _client.Sched.DeletePath[s2.Unid!.Value.ToString()].PostAsync();
        await _client.Sched.DeletePath[s3.Unid!.Value.ToString()].PostAsync();
    }

    // --- Upsert ---

    private async Task TestUpsertSched()
    {
        var newUuid = Guid.NewGuid().ToString();

        // Upsert with a UUID that doesn't exist — should create
        var upserted = await _client.Sched.Update[newUuid]
            .PostAsUpdatePostResponseAsync(
                new Sched { Name = "Upserted Sched", Tag = "ups" },
                cfg => { cfg.QueryParameters.Upsert = true; });
        Assert.That(upserted!.Instance!.Name, Is.EqualTo("Upserted Sched"));
        Assert.That(upserted.Instance.Unid, Is.Not.Null);
        Assert.That(upserted.Instance.Uuid, Is.EqualTo(newUuid));

        // Verify it shows up in list
        var list = await _client.Sched.List.GetAsync();
        Assert.That(list!.InstanceList!.Any(s => s.Uuid == newUuid), Is.True);

        // Update same UUID again (now it exists) — should update in place
        var updated = await _client.Sched.Update[newUuid]
            .PostAsUpdatePostResponseAsync(
                new Sched { Name = "Re-updated Sched" },
                cfg => { cfg.QueryParameters.Upsert = true; });
        Assert.That(updated!.Instance!.Name, Is.EqualTo("Re-updated Sched"));
        Assert.That(updated.Instance.Uuid, Is.EqualTo(newUuid));

        // Count should still be the same (no duplicate created)
        var list2 = await _client.Sched.List.GetAsync();
        var matchCount = list2!.InstanceList!.Count(s => s.Uuid == newUuid);
        Assert.That(matchCount, Is.EqualTo(1));

        // Cleanup
        await _client.Sched.DeletePath[newUuid].PostAsync();
        Assert.That((await _client.Sched.List.GetAsync())!.InstanceList!.Any(s => s.Uuid == newUuid), Is.False);
    }
}
