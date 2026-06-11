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

    [Theory]
    [InlineData("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("json", "application/json")]
    [InlineData("md", "text/markdown")]
    public async Task Download_AfterAssess_ReturnsArtifact(string format, string expectedContentType)
    {
        var client = _factory.CreateClient();
        var assess = await client.PostAsync("/api/assess",
            new StringContent(SampleIntake, Encoding.UTF8, "application/json"));
        var payload = await assess.Content.ReadFromJsonAsync<JsonElement>();
        var id = payload.GetProperty("id").GetString();

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
}
