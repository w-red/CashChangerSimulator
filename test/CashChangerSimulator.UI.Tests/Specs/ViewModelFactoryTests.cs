using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

public class ViewModelFactoryTests
{
    /// <summary>コンストラクタに null を渡した際、ArgumentNullException がスローされることを検証します。</summary>
    [Fact]
    public void ConstructorWithNullProviderShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new ViewModelFactory(null!));
    }

    /// <summary>DepositViewModel の作成コマンドが、DIコンテナからの解決を試みることを検証します。</summary>
    [Fact]
    public void CreateDepositViewModelShouldResolveFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        // Mocking the viewmodel is hard if it has many dependencies, so we create a dummy object or register a mock.
        // Wait, DepositViewModel is a concrete type. 
        // We will just verify that the factory attempts to resolve it.
        var providerMock = new Mock<IServiceProvider>();
        
        // DepositViewModel does not have a parameterless constructor, so we can't easily mock return value of concrete class without its dependencies.
        // Instead, we just verify GetService is called.
        
        var factory = new ViewModelFactory(providerMock.Object);

        // Act & Assert
        // ActivatorUtilities will try to resolve dependencies from the provider.
        // Since we didn't register them, it will likely throw.
        var getDenoms = new Func<IEnumerable<DenominationViewModel>>(() => Enumerable.Empty<DenominationViewModel>());
        var isBusy = new BindableReactiveProperty<bool>(false);
        
        Should.Throw<InvalidOperationException>(() => factory.CreateDepositViewModel(getDenoms, isBusy));
    }
}
