using System.Reflection;

namespace CashRegister.Tests;

public sealed class ScaffoldSmokeTests
{
    [Fact]
    public void CashRegisterModuleAssembly_IsReachableFromTheUnitTestProject()
    {
        // Loaded by name: the module has no types yet, so a compile-time
        // reference would be dropped from the test assembly's metadata.
        var load = () => Assembly.Load("CashRegister");

        load.ShouldNotThrow();
    }
}
