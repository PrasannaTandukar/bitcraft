using Bitcraft.Application;
using Bitcraft.Infrastructure;
using Bitcraft.Infrastructure.Identity;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

    // Include XML docs from Api, Domain (enums/value types surface in contracts) so the
    // generated schemas carry descriptions for the front-end / AI clients.
    foreach (var assemblyName in new[] { "Bitcraft.Api", "Bitcraft.Domain" })
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
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
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

app.UseAuthentication();
app.UseCors("AllowFrontend");
app.UseAuthorization();

app.MapControllers();

app.Run();
