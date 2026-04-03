namespace EquiLink.Api.Endpoints;

public static class HealthCheckEndpoints
{
    public static void MapHealthChecksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }))
            .WithTags("Health")
            .WithName("HealthCheck")
            .WithOpenApi();
    }
}
