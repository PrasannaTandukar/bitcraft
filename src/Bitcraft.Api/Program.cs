using Bitcraft.Application;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure;
using Bitcraft.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;
using Prometheus;
using Microsoft.AspNetCore.HttpLogging;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.ResponseStatusCode;
});
var configuredUploadBytes = builder.Configuration.GetValue<long?>("Documents:MaxUploadBytes");
var maximumUploadBytes = configuredUploadBytes is > 0
    ? configuredUploadBytes.Value
    : DocumentIngestionLimits.DefaultMaxUploadBytes;
builder.Services.Configure<FormOptions>(options =>
{
    // Permit a small amount of multipart framing overhead while the controller
    // independently enforces the exact per-file byte limit.
    options.MultipartBodyLengthLimit = maximumUploadBytes + (64 * 1024);
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bitcraft Tutor Studio API",
        Version = "v1",
        Description =
            "REST surface for managing AI tutors: prompts, model connections, knowledge sources, " +
            "tools, deployment profiles, and reproducible configuration snapshots used for benchmarking. " +
            "Management endpoints require authenticated access and role policies."
    });

    // Include XML docs from Api plus referenced backend layers so generated schemas
    // can surface descriptions from shared enums/value types and service models.
    foreach (var assemblyName in new[] { "Bitcraft.Api", "Bitcraft.Application", "Bitcraft.Domain" })
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }

    // JWT bearer auth for Swagger UI.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a JWT obtained from /api/auth/login."
    });
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });

    c.SupportNonNullableReferenceTypes();
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:8080",
                "http://127.0.0.1:8080")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Global IP-based fixed-window rate limit (Iteration 1 / Pre-Final Plan v3.6).
// Defaults: 10 requests per second per remote IP. Returns HTTP 429 on rejection.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        await IdentityRoleSeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Role seeding failed — continuing startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.UseRouting();
app.UseHttpLogging(); // Individual HTTP request logs
app.UseHttpMetrics();   // request count and duration by status code, method, route

// Serve /metrics on a separate internal-only port, not on the public API port.
//new KestrelMetricServer(port: 9464).Start();
// Serve /metrics on a separate internal-only port.
// Disable the listener in integration tests to avoid port conflicts.
if (app.Configuration.GetValue<bool>("Metrics:Enabled"))
{
    var metricsPort = app.Configuration.GetValue<int?>("Metrics:Port") ?? 9464;
    new KestrelMetricServer(port: metricsPort).Start();
}

app.UseAuthentication();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
