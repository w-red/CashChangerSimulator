using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using Shouldly;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliDIContainer のサービス登録機能を検証するためのテストクラス。</summary>
public class CliDIContainerTests
{
    /// <summary>全ての必須サービスが DI コンテナに登録されていることを検証します。</summary>
    [Fact]
    public void ShouldRegisterRequiredServices()
    {
        // Arrange & Act
        var builder = Host.CreateApplicationBuilder();
        CliDIContainer.ConfigureServices(builder.Services, []);
        var provider = builder.Services.BuildServiceProvider();

        // Assert
        provider.GetService<SimulatorCashChanger>().ShouldNotBeNull();
        provider.GetService<ICliCommandDispatcher>().ShouldNotBeNull();
        provider.GetService<CliDeviceService>().ShouldNotBeNull();
        provider.GetService<CliCashService>().ShouldNotBeNull();
        provider.GetService<CliConfigService>().ShouldNotBeNull();
        provider.GetService<CliViewService>().ShouldNotBeNull();
        provider.GetService<CliScriptService>().ShouldNotBeNull();
        provider.GetService<CliInteractiveShell>().ShouldNotBeNull();
        provider.GetService<CliCommands>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldInitializeSuccessfully()
    {
        // Act
        // This hits Initialize() and PostInitialize() with some args
        var args = new[] { "--verbose", "--currency", "USD" };
        CliDIContainer.Initialize(args);
        CliDIContainer.PostInitialize(CliDIContainer.ServiceProvider, args);

        // Assert
        CliDIContainer.ServiceProvider.ShouldNotBeNull();
        
        var configProvider = CliDIContainer.Resolve<Core.Configuration.ConfigurationProvider>();
        configProvider.ShouldNotBeNull();
        configProvider.Config.System.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void ResolverServiceProvider_ShouldResolveDependencies()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        CliDIContainer.ConfigureServices(builder.Services, []);
        var provider = builder.Services.BuildServiceProvider();

        // This effectively instantiates the internal `CliResolverServiceProvider` indirectly,
        // but we can explicitly test it by using the SimulatorServices abstraction.
        CliDIContainer.Initialize([]);
        
        // Act
        var providerCasted = (IServiceProvider)CashChangerSimulator.Core.SimulatorServices.Provider!;
        var config = CashChangerSimulator.Core.SimulatorServices.Provider!.Resolve<Core.Configuration.ConfigurationProvider>();
        var obj = providerCasted.GetService(typeof(Core.Configuration.ConfigurationProvider));

        // Assert
        config.ShouldNotBeNull();
        Assert.NotNull(obj);
    }
}
