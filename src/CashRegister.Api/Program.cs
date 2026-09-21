var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

await app.RunAsync();

/// <summary>Entry point, exposed for <c>WebApplicationFactory&lt;Program&gt;</c> in the integration tests.</summary>
public partial class Program;
