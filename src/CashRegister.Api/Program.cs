using CashRegister.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddCashRegisterHost();

var app = builder.Build();
app.UseCashRegisterHost();

await app.RunAsync();

/// <summary>Entry point, exposed for <c>WebApplicationFactory&lt;Program&gt;</c> in the integration tests.</summary>
public partial class Program;
