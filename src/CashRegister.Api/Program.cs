using CashRegister;

const string SpaCorsPolicy = "Spa";

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddJsonConsole(); // ADR-008: structured logs outside Development
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler(options =>
    // Malformed request bodies surface as BadHttpRequestException (thrown in Development);
    // report them with their own 400 status rather than as a 500.
    options.StatusCodeSelector = exception =>
        exception is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError);
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddCashRegister(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(SpaCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapCashRegister();

await app.RunAsync();

/// <summary>Entry point, exposed for <c>WebApplicationFactory&lt;Program&gt;</c> in the integration tests.</summary>
public partial class Program;
