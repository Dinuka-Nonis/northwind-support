using Microsoft.OpenApi;

namespace SupportDesk.Presentation.Extensions;

public static class SwaggerExtensions
{
    /// <summary>
    /// Adds Swagger with an Authorize button, so the endpoints can be tried with the token that
    /// <c>POST /api/auth/login</c> returns.
    /// </summary>
    public static IServiceCollection AddSwaggerWithBearerAuth(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the token from POST /api/auth/login (without the word Bearer)."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        return services;
    }
}
