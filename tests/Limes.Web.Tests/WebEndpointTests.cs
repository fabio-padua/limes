using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Limes.Web.Tests;

public sealed class WebEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private const string SampleIntake = """
        {
          "partner": { "name": "Contoso Cloud Solutions", "region": "Brazil", "industry": "Financial Services" },
          "pillars": [
            { "pillar": "BusinessStrategy", "responses": [
              { "questionId": "BS1", "prompt": "Tied to value.", "score": 4 },
              { "questionId": "BS2", "prompt": "Sponsorship.", "score": 3 } ] },
            { "pillar": "GovernanceAndSecurity", "responses": [
              { "questionId": "GS1", "prompt": "RAI documented.", "score": 2 } ] }
          ]
        }
        """;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Sample_ReturnsBundledIntake()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/sample");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("partner", out _));
        Assert.True(doc.RootElement.TryGetProperty("pillars", out _));
    }

    [Fact]
    public async Task Assess_WithValidIntake_ReturnsScoredResult()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/assess",
            new StringContent(SampleIntake, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.False(string.IsNullOrEmpty(root.GetProperty("id").GetString()));
        Assert.Equal("Contoso Cloud Solutions", root.GetProperty("partner").GetProperty("name").GetString());
        var index = root.GetProperty("readinessIndex").GetDouble();
        Assert.InRange(index, 1.0, 5.0);
        // The pipeline always scores the full set of seven AI Readiness pillars,
        // regardless of how many the intake supplies answers for.
        Assert.Equal(7, root.GetProperty("pillars").GetArrayLength());
    }

    [Fact]
    public async Task Assess_WithInvalidJson_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/assess",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Assess_WithEmptyBody_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/assess", new StringContent("", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Assess_WithOversizedBody_ReturnsPayloadTooLarge()
    {
        var client = _factory.CreateClient();
        // Exceed the 1 MB cap with padded JSON; the guard should reject before parsing.
        var huge = "{\"partner\":{\"name\":\"" + new string('a', 1_100_000) + "\"}}";
        var res = await client.PostAsync("/api/assess",
            new StringContent(huge, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
    }

    [Theory]
    [InlineData("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("json", "application/json")]
    [InlineData("md", "text/markdown")]
    [InlineData("html", "text/html")]
    public async Task Download_AfterAssess_ReturnsArtifact(string format, string expectedContentType)
    {
        var client = _factory.CreateClient();
        var assess = await client.PostAsync("/api/assess",
            new StringContent(SampleIntake, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, assess.StatusCode);
        var payload = await assess.Content.ReadFromJsonAsync<JsonElement>();
        var id = payload.GetProperty("id").GetString();
        Assert.False(string.IsNullOrEmpty(id));

        var res = await client.GetAsync($"/api/assessments/{id}/download/{format}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(expectedContentType, res.Content.Headers.ContentType?.MediaType);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task Download_UnknownId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/assessments/does-not-exist/download/docx");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Questionnaire_ReturnsSevenPillarsAndLevels()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/questionnaire");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var pillars = root.GetProperty("pillars");
        Assert.Equal(7, pillars.GetArrayLength());
        // The five maturity levels (Initial..Optimized) are the 1-5 rating labels.
        Assert.Equal(5, root.GetProperty("levels").GetArrayLength());

        foreach (var pillar in pillars.EnumerateArray())
        {
            // Raw enum name (for intake assembly) and a friendly heading must both be present.
            Assert.False(string.IsNullOrEmpty(pillar.GetProperty("pillar").GetString()));
            Assert.False(string.IsNullOrEmpty(pillar.GetProperty("displayName").GetString()));
            var questions = pillar.GetProperty("questions");
            Assert.True(questions.GetArrayLength() > 0);
            foreach (var q in questions.EnumerateArray())
            {
                Assert.False(string.IsNullOrEmpty(q.GetProperty("id").GetString()));
                Assert.False(string.IsNullOrEmpty(q.GetProperty("prompt").GetString()));
            }
        }
    }

    [Fact]
    public async Task Assess_AgentsModeWithoutFoundry_ReturnsConflict()
    {
        // Clear any ambient Foundry config so the test is environment-independent: with no
        // endpoint/deployment set, agents mode must fail clearly (409) rather than attempting
        // a real Foundry call (or silently downgrading to deterministic).
        var savedEndpoint = Environment.GetEnvironmentVariable("LIMES_FOUNDRY_ENDPOINT");
        var savedDeployment = Environment.GetEnvironmentVariable("LIMES_FOUNDRY_DEPLOYMENT");
        Environment.SetEnvironmentVariable("LIMES_FOUNDRY_ENDPOINT", null);
        Environment.SetEnvironmentVariable("LIMES_FOUNDRY_DEPLOYMENT", null);
        try
        {
            var client = _factory.CreateClient();
            var res = await client.PostAsync("/api/assess?mode=agents",
                new StringContent(SampleIntake, Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("error").GetString()));
            Assert.True(doc.RootElement.TryGetProperty("hint", out _));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LIMES_FOUNDRY_ENDPOINT", savedEndpoint);
            Environment.SetEnvironmentVariable("LIMES_FOUNDRY_DEPLOYMENT", savedDeployment);
        }
    }

    [Fact]
    public async Task Assess_AgentsModeWithInvalidEndpoint_DoesNotLeakConfiguredValue()
    {
        // An invalid (non-HTTPS) endpoint must not be echoed back to the caller: the 409 detail
        // should be generic so server configuration isn't leaked. The raw value is logged server-side.
        const string secretHost = "internal-foundry-host.contoso.local";
        var savedEndpoint = Environment.GetEnvironmentVariable("LIMES_FOUNDRY_ENDPOINT");
        var savedDeployment = Environment.GetEnvironmentVariable("LIMES_FOUNDRY_DEPLOYMENT");
        Environment.SetEnvironmentVariable("LIMES_FOUNDRY_ENDPOINT", $"http://{secretHost}/openai");
        Environment.SetEnvironmentVariable("LIMES_FOUNDRY_DEPLOYMENT", "gpt-4.1");
        try
        {
            var client = _factory.CreateClient();
            var res = await client.PostAsync("/api/assess?mode=agents",
                new StringContent(SampleIntake, Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var payload = await res.Content.ReadAsStringAsync();
            Assert.DoesNotContain(secretHost, payload);
            using var doc = JsonDocument.Parse(payload);
            Assert.Equal("LIMES_FOUNDRY_ENDPOINT is set but invalid.",
                doc.RootElement.GetProperty("detail").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LIMES_FOUNDRY_ENDPOINT", savedEndpoint);
            Environment.SetEnvironmentVariable("LIMES_FOUNDRY_DEPLOYMENT", savedDeployment);
        }
    }

    [Theory]
    [InlineData("agent")]   // typo for "agents"
    [InlineData("foo")]
    public async Task Assess_UnknownMode_ReturnsBadRequest(string mode)
    {
        // An unknown mode is a 400, not a silent downgrade to deterministic.
        var client = _factory.CreateClient();
        var res = await client.PostAsync($"/api/assess?mode={mode}",
            new StringContent(SampleIntake, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Assess_ExplicitDeterministicMode_ReturnsScoredResult()
    {
        // The deterministic mode value is accepted explicitly (not just the empty default).
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/assess?mode=deterministic",
            new StringContent(SampleIntake, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
