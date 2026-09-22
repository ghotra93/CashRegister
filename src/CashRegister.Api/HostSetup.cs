namespace CashRegister.Api;

/// <summary>
/// Host composition: everything <c>Program.cs</c> used to decide inline, as extension methods and
/// small functions that unit tests can call (code review F-001).
/// </summary>
public static class HostSetup
{
    /// <summary>The CORS policy that admits the configured UI origins (ADR-005).</summary>
    public const string SpaCorsPolicy = "Spa";

    private const string AllowedOriginsKey = "Cors:AllowedOrigins";
    private const string HealthPath = "/health";

    /// <summary>Registers logging, error handling, health, OpenAPI, CORS and the CashRegister module.</summary>
    public static WebApplicationBuilder AddCashRegisterHost(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.AddJsonConsole(); // ADR-008: structured logs outside Development
        }

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler(options => options.StatusCodeSelector = StatusCodeFor);
        builder.Services.AddHealthChecks();
        builder.Services.AddOpenApi();

        var allowedOrigins = AllowedOrigins(builder.Configuration);
        builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

        builder.Services.AddCashRegister(builder.Configuration);
        return builder;
    }

    /// <summary>Builds the request pipeline and maps health, OpenAPI (Development only) and the module endpoints.</summary>
    public static WebApplication UseCashRegisterHost(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors(SpaCorsPolicy);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapHealthChecks(HealthPath);
        app.MapCashRegister();
        return app;
    }

    /// <summary>
    /// Status code for an unhandled exception. Malformed request bodies surface as
    /// <see cref="BadHttpRequestException"/> (thrown in Development) and keep their own 4xx status;
    /// anything else is a 500.
    /// </summary>
    internal static int StatusCodeFor(Exception exception)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            return badRequest.StatusCode;
        }

        return StatusCodes.Status500InternalServerError;
    }

    /// <summary>Origins allowed to call the API from a browser; none when the section is absent.</summary>
    internal static string[] AllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];
}
