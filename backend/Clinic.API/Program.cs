using Clinic.API;
using Clinic.API.Hubs;
using Clinic.API.Workers;
using Clinic.Application;
using Clinic.Infrastructure.Sqlserver;
using Clinic.Infrastructure.Sqlserver.Notifications;
using Clinic.Infrastructure.Sqlserver.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi;
using System.Reflection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "https://localhost:5173",
                "https://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add services to the container.
builder.Services.AddPresentation();
builder.Services.AddApplication();
builder.Services.AddInfrastructureSqlServer(builder.Configuration);

// Configure JSON serialization options to handle enum values as strings
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configure notification services
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection("Email"));
builder.Services.AddSignalR();

// Add workers
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddHostedService<ScheduledNotificationWorker>();

// Add Authentication and Authorization services
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            RoleClaimType = ClaimTypes.Role
        };
    });
// Register permission policy provider and handler for dynamic permission policies
builder.Services.AddAuthorization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Clinic.API.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Clinic.API.Authorization.PermissionAuthorizationHandler>();

// Register current-user accessor (đọc UserId từ ClaimsPrincipal của request hiện tại)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Clinic.Application.Interfaces.ICurrentUser, Clinic.API.Authentication.CurrentUser>();

// Cấu hình business rule của Appointment (vd: số giờ tối thiểu Patient phải đổi lịch trước hạn), đọc từ section "Appointment".
builder.Services.Configure<Clinic.API.Configuration.AppointmentPolicySettings>(
    builder.Configuration.GetSection(Clinic.API.Configuration.AppointmentPolicySettings.SectionName));
builder.Services.AddScoped<Clinic.Application.Interfaces.IAppointmentPolicySettings>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Clinic.API.Configuration.AppointmentPolicySettings>>().Value);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] =
                new List<string>()
        });

    // Đọc file XML comment sinh ra từ bước 1, để Swagger UI hiện summary/description
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.MapHub<NotificationHub>("/hubs/notifications");

// Seed the database with an initial admin user if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider
        .GetRequiredService<DatabaseInitializer>();

    await initializer.CreateInitialAdminAsync();
}

// Bắt mọi exception chưa xử lý và trả về envelope ApiResponse (qua GlobalExceptionHandler).
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("FrontendCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();