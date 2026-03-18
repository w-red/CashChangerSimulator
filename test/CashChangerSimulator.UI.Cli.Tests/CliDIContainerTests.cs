using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CashChangerSimulator.UI.Cli;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using Xunit;
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
}
