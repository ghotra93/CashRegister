using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using CashRegister.Features.Change.Rules;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CashRegister.Tests.Architecture;

/// <summary>
/// Executable versions of the module boundaries in 03-design.md. Each "must not depend"
/// rule has a positive control proving the dependency is visible to ArchUnitNET, so the
/// rule cannot pass vacuously.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly System.Reflection.Assembly ModuleAssembly = typeof(CashRegisterModule).Assembly;

    // ASP.NET Core HTTP assemblies are loaded too: without them their types are invisible and
    // the "only endpoints use Microsoft.AspNetCore.Http" rule would pass vacuously.
    private static readonly ArchUnitNET.Domain.Architecture Module =
        new ArchLoader().LoadAssemblies(
                ModuleAssembly,
                typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly,
                typeof(Microsoft.AspNetCore.Http.IFormFile).Assembly,
                typeof(Microsoft.AspNetCore.Http.TypedResults).Assembly,
                typeof(Microsoft.AspNetCore.Http.StatusCodes).Assembly)
            .Build();

    private const string CurrenciesNamespace = "CashRegister.Features.Currencies";
    private const string ChangeNamespace = "CashRegister.Features.Change";
    private const string FeaturesNamespace = "CashRegister.Features";
    private const string AspNetCoreHttpNamespace = "Microsoft.AspNetCore.Http";

    private static GivenTypesConjunction TypesIn(string rootNamespace) =>
        Types().That().ResideInNamespaceMatching($@"^{rootNamespace.Replace(".", @"\.")}(\..*)?$");

    [Fact]
    public void Currencies_DoNotDependOnChange()
    {
        TypesIn(CurrenciesNamespace).Should().NotDependOnAny(TypesIn(ChangeNamespace)).Check(Module);
    }

    [Fact]
    public void Change_DependsOnCurrencies_PositiveControl()
    {
        Types().That().HaveFullName(typeof(CashRegister.Features.Change.Money.Transaction).FullName!)
            .Should().DependOnAny(TypesIn(CurrenciesNamespace))
            .Check(Module);
    }

    [Fact]
    public void Module_DoesNotReferenceTheApiHost()
    {
        ModuleAssembly.GetReferencedAssemblies().Select(a => a.Name).ShouldNotContain("CashRegister.Api");
    }

    [Fact]
    [Trait("AC", "AC-024")]
    public void ChangeRules_AreSealedAndLiveInTheRulesNamespace()
    {
        Classes().That().ImplementInterface(typeof(IChangeRule))
            .Should().BeSealed()
            .AndShould().ResideInNamespace("CashRegister.Features.Change.Rules")
            .Check(Module);
    }

    [Fact]
    public void Module_DoesNotReadTheSystemClock()
    {
        Types().Should().NotCallAny(
                MethodMembers().That().HaveFullNameContaining("System.DateTime::get_Now")
                    .Or().HaveFullNameContaining("System.DateTime::get_UtcNow")
                    .Or().HaveFullNameContaining("System.DateTimeOffset::get_Now")
                    .Or().HaveFullNameContaining("System.DateTimeOffset::get_UtcNow"))
            .Because("time comes from TimeProvider so tests can control it")
            .Check(Module);
    }

    [Fact]
    public void Features_OnlyEndpointsUseAspNetCoreHttp()
    {
        TypesIn(FeaturesNamespace).And().DoNotHaveNameEndingWith("Endpoints")
            .Should().NotDependOnAny(TypesIn(AspNetCoreHttpNamespace))
            .Check(Module);
    }

    [Fact]
    public void Endpoints_UseAspNetCoreHttp_PositiveControl()
    {
        TypesIn(FeaturesNamespace).And().HaveNameEndingWith("Endpoints")
            .Should().DependOnAny(TypesIn(AspNetCoreHttpNamespace))
            .Check(Module);
    }
}
