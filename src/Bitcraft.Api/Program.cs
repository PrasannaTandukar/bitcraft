using Bitcraft.Application;
using Bitcraft.Infrastructure;
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
            "Phase A requires authenticated access for management endpoints; Phase B adds role policies."
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
