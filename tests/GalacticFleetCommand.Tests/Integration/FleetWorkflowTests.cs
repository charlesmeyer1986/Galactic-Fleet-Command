using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GalacticFleetCommand.Tests.Integration;

public class FleetWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FleetWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task FullFleetLifecycle_CreatePrepareDeployTimeline()
    {
        // 1. Create fleet
        var createResponse = await _client.PostAsJsonAsync("/fleets", new
        {
            name = "Alpha Fleet",
            ships = new[] { new { name = "Destroyer-1", type = "Destroyer" } },
            resourceRequirements = new[] { new { type = "Fuel", quantity = 10 }, new { type = "BattleDroids", quantity = 5 } }
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var fleet = await createResponse.Content.ReadFromJsonAsync<FleetDto>(JsonOptions);
        Assert.NotNull(fleet);
        Assert.Equal("Docked", fleet!.State);
        var fleetId = fleet.Id;

        // 2. Modify fleet (PATCH)
        var patchResponse = await _client.PatchAsJsonAsync($"/fleets/{fleetId}", new
        {
            version = fleet.Version,
            name = "Alpha Fleet - Updated"
        });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updatedFleet = await patchResponse.Content.ReadFromJsonAsync<FleetDto>(JsonOptions);
        Assert.Equal("Alpha Fleet - Updated", updatedFleet!.Name);

        // 3. Submit PrepareFleet command
        var prepResponse = await _client.PostAsJsonAsync("/commands", new
        {
            type = "PrepareFleet",
            payload = new { fleetId },
            idempotencyKey = $"PrepareFleet:{fleetId}:workflow-test"
        });
        Assert.Equal(HttpStatusCode.Accepted, prepResponse.StatusCode);

        var prepCmd = await prepResponse.Content.ReadFromJsonAsync<CommandDto>(JsonOptions);
        Assert.NotNull(prepCmd);

        // 4. Poll for command completion
        var prepResult = await PollCommandUntilDone(prepCmd!.Id);
        Assert.Equal("Succeeded", prepResult.Status);

        // 5. Verify fleet is Ready
        var getFleetResponse = await _client.GetAsync($"/fleets/{fleetId}");
        Assert.Equal(HttpStatusCode.OK, getFleetResponse.StatusCode);
        var readyFleet = await getFleetResponse.Content.ReadFromJsonAsync<FleetDto>(JsonOptions);
        Assert.Equal("Ready", readyFleet!.State);

        // 6. Submit DeployFleet command
        var deployResponse = await _client.PostAsJsonAsync("/commands", new
        {
            type = "DeployFleet",
            payload = new { fleetId },
            idempotencyKey = $"DeployFleet:{fleetId}:workflow-test"
        });
        Assert.Equal(HttpStatusCode.Accepted, deployResponse.StatusCode);

        var deployCmd = await deployResponse.Content.ReadFromJsonAsync<CommandDto>(JsonOptions);

        // 7. Poll for command completion
        var deployResult = await PollCommandUntilDone(deployCmd!.Id);
        Assert.Equal("Succeeded", deployResult.Status);

        // 8. Verify fleet is Deployed
        getFleetResponse = await _client.GetAsync($"/fleets/{fleetId}");
        var deployedFleet = await getFleetResponse.Content.ReadFromJsonAsync<FleetDto>(JsonOptions);
        Assert.Equal("Deployed", deployedFleet!.State);

        // 9. Check timeline
        var timelineResponse = await _client.GetAsync($"/fleets/{fleetId}/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);

        var timeline = await timelineResponse.Content.ReadFromJsonAsync<List<TimelineEventDto>>(JsonOptions);
        Assert.NotNull(timeline);
        Assert.True(timeline!.Count >= 4, $"Expected at least 4 timeline events, got {timeline.Count}");
    }

    [Fact]
    public async Task PatchNonDockedFleet_Returns409()
    {
        // Create and prepare a fleet
        var createResponse = await _client.PostAsJsonAsync("/fleets", new
        {
            name = "Locked Fleet",
            resourceRequirements = new[] { new { type = "Fuel", quantity = 1 } }
        });
        var fleet = await createResponse.Content.ReadFromJsonAsync<FleetDto>(JsonOptions);

        var prepResponse = await _client.PostAsJsonAsync("/commands", new
        {
            type = "PrepareFleet",
            payload = new { fleetId = fleet!.Id },
            idempotencyKey = $"PrepareFleet:{fleet.Id}:lock-test"
        });
        var prepCmd = await prepResponse.Content.ReadFromJsonAsync<CommandDto>(JsonOptions);
        await PollCommandUntilDone(prepCmd!.Id);

        // Get current version
        var getResp = await _client.GetAsync($"/fleets/{fleet.Id}");
        var readyFleet = await getResp.Content.ReadFromJsonAsync<FleetDto>(JsonOptions);

        // Try to patch — should fail with 409
        var patchResponse = await _client.PatchAsJsonAsync($"/fleets/{fleet.Id}", new
        {
            version = readyFleet!.Version,
            name = "Should Fail"
        });
        Assert.Equal(HttpStatusCode.Conflict, patchResponse.StatusCode);
    }

    [Fact]
    public async Task GetNonExistentFleet_Returns404()
    {
        var response = await _client.GetAsync($"/fleets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IdempotentCommand_ReturnsSameResult()
    {
        var createResponse = await _client.PostAsJsonAsync("/fleets", new
        {
            name = "Idempotent Fleet",
            resourceRequirements = new[] { new { type = "Fuel", quantity = 1 } }
        });
        var fleet = await createResponse.Content.ReadFromJsonAsync<FleetDto>(JsonOptions);

        var idempotencyKey = $"PrepareFleet:{fleet!.Id}:idem-test";

        // First submission
        var resp1 = await _client.PostAsJsonAsync("/commands", new
        {
            type = "PrepareFleet",
            payload = new { fleetId = fleet.Id },
            idempotencyKey
        });
        var cmd1 = await resp1.Content.ReadFromJsonAsync<CommandDto>(JsonOptions);
        await PollCommandUntilDone(cmd1!.Id);

        // Second submission with same idempotency key — should return the existing succeeded command
        var resp2 = await _client.PostAsJsonAsync("/commands", new
        {
            type = "PrepareFleet",
            payload = new { fleetId = fleet.Id },
            idempotencyKey
        });
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        var cmd2 = await resp2.Content.ReadFromJsonAsync<CommandDto>(JsonOptions);
        Assert.Equal(cmd1.Id, cmd2!.Id);
    }

    private async Task<CommandDto> PollCommandUntilDone(Guid commandId, int maxWaitMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            var response = await _client.GetAsync($"/commands/{commandId}");
            var cmd = await response.Content.ReadFromJsonAsync<CommandDto>(JsonOptions);
            if (cmd!.Status is "Succeeded" or "Failed")
                return cmd;
            await Task.Delay(50);
        }

        throw new TimeoutException($"Command {commandId} did not complete within {maxWaitMs}ms");
    }

    private record FleetDto(Guid Id, int Version, string Name, string State);
    private record CommandDto(Guid Id, string CommandType, string Status);
    private record TimelineEventDto(Guid Id, Guid FleetId, string Type, DateTime Timestamp);
}
