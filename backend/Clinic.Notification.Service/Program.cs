var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "Clinic.Notification.Service",
    status = "Scaffold",
    version = "1.0.0"
}));
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.Run();

public partial class Program { }
