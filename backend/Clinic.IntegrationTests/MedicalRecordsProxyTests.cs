using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Clinic.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Clinic.IntegrationTests;

public class MedicalRecordsProxyTests
{
    [Fact]
    public async Task MedicalRecordsServiceClient_ForwardsHeadersQueryAndBody()
    {
        var capturingHandler = new CapturingHttpMessageHandler();
        var factory = new TestClientFactory(capturingHandler);
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var client = new MedicalRecordsServiceClient(factory, config);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/medical-reports";
        context.Request.QueryString = new QueryString("?isUrgent=true");
        context.Request.Headers["Authorization"] = "Bearer test-jwt-token";
        context.Request.Headers["Idempotency-Key"] = "idemp-key-123";

        var payload = new { symptoms = "Cough", diagnosis = "Cold" };
        var json = JsonSerializer.Serialize(payload);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentLength = Encoding.UTF8.GetByteCount(json);
        context.Request.ContentType = "application/json";

        var result = await client.ProxyAsync(context, "medical-records", HttpMethod.Post);

        Assert.IsType<ContentResult>(result);
        Assert.NotNull(capturingHandler.LastRequest);
        Assert.Equal("Bearer test-jwt-token", capturingHandler.LastRequest.Headers.Authorization?.ToString());
        Assert.True(capturingHandler.LastRequest.Headers.Contains("Idempotency-Key"));
        Assert.Equal("/medical-records?isUrgent=true", capturingHandler.LastRequest.RequestUri?.PathAndQuery);
        Assert.Contains("Cough", capturingHandler.LastRequestBody);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TestClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost:5005/")
        };
    }
}
