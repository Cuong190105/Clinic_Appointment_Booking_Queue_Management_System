using System.Net;
using System.Text;
using Clinic.API.Middlewares;
using Clinic.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.IntegrationTests;

public sealed class SchedulingProxyMiddlewareTests
{
    [Fact]
    public async Task PostJson_ForwardsContentTypeAndBodyToSchedulingService()
    {
        var handler = new CapturingHandler();
        var clientFactory = new TestHttpClientFactory(handler);
        var identityClient = new IdentityAccountClient(clientFactory, new ConfigurationBuilder().Build());
        var middleware = new SchedulingProxyMiddleware(
            _ => Task.CompletedTask,
            clientFactory,
            identityClient,
            NullLogger<SchedulingProxyMiddleware>.Instance);
        var payload = "{\"date\":\"2026-11-12\",\"startTime\":\"09:00:00\",\"endTime\":\"12:00:00\",\"recommendedCapacity\":20}";
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/doctors/11111111-1111-1111-1111-111111111111/shifts";
        context.Request.ContentType = "application/json; charset=utf-8";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.ContentLength = Encoding.UTF8.GetByteCount(payload);
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal("application/json", handler.ContentType?.MediaType);
        Assert.Equal("utf-8", handler.ContentType?.CharSet);
        Assert.Equal(payload, handler.Body);
        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public System.Net.Http.Headers.MediaTypeHeaderValue? ContentType { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ContentType = request.Content?.Headers.ContentType;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://scheduling.test")
        };
    }
}
