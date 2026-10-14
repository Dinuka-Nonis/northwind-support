using System.Text.Json.Serialization;
using SupportDesk.Application;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddSwaggerWithBearerAuth();

// Token issuing and bearer validation. The Jwt settings (including the signing key) are checked
// here, so a missing or weak key stops the app at startup.
builder.Services.AddJwtAuthentication(builder.Configuration);

// The SLA rules live in configuration, not code. A missing or broken section stops the app at
// startup instead of failing on the first ticket.
var slaPolicy = builder.Configuration.GetSection("Sla").Get<SlaPolicy>()
    ?? throw new InvalidOperationException("The 'Sla' configuration section is missing.");
slaPolicy.EnsureValid();
builder.Services.AddSingleton(slaPolicy);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Keeps a developer machine one command away from a working database.
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

// Authentication reads the token and sets the user; authorization then decides what that user
// may reach. Both come after CORS (so preflight requests are answered) and before the endpoints.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
