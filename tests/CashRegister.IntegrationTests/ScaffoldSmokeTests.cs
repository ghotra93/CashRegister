namespace CashRegister.IntegrationTests;

public sealed class ScaffoldSmokeTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void ApiHostProgram_IsReachableFromTheIntegrationTestProject()
    {
        var program = Type.GetType("Program, CashRegister.Api");

        program.ShouldNotBeNull("the integration test project must reference the CashRegister.Api host");
    }
}
