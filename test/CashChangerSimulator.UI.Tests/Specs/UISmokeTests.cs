using System.Windows;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.Controls;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using Shouldly;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using R3;

using CashChangerSimulator.Core.Transactions;
using MaterialDesignThemes.Wpf;
using MaterialDesignColors;
using CashChangerSimulator.Core.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.UI.Wpf.Converters;
using System.IO.Packaging;
using System.Windows.Controls;
using CashChangerSimulator.UI.Tests.Helpers;

namespace CashChangerSimulator.UI.Tests.Specs;

public class UISmokeTests
{
    [Fact]
    public void AllMajorViewsShouldLoadWithoutXamlException()
    {
        var thread = new Thread(() =>
        {
            try
            {
                // Ensure pack:// support
                if (!UriParser.IsKnownScheme("pack"))
                {
                    _ = PackUriHelper.UriSchemePack;
                }

                // Ensure Application instance exists
                if (Application.Current == null)
                {
                    new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                }

                // Load Global Resources manually to avoid App.xaml x:Class issues
                var app = Application.Current;
                if (app == null) throw new Exception("Application.Current is null.");
                var resources = app.Resources.MergedDictionaries;
                resources.Clear();
                
                // BundledTheme handles multiple necessary dictionaries for MaterialDesign
                resources.Add(new BundledTheme
                {
                    BaseTheme = BaseTheme.Dark,
                    PrimaryColor = PrimaryColor.DeepPurple,
                    SecondaryColor = SecondaryColor.Lime
                });

                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml") });
                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/Colors.xaml") });
                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/Strings.en-US.xaml") });
                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/Styles.xaml") });
                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/Templates.xaml") });

                // Register standard converters used in XAML
                app.Resources["StatusBrushConv"] = new CashStatusToBrushConverter();
                app.Resources["TypeSymbolConv"] = new TransactionTypeToSymbolConverter();
                app.Resources["InvertedVisibilityConv"] = new InvertedBooleanToVisibilityConverter();
                app.Resources["InverseBoolConverter"] = new InverseBooleanConverter();
                app.Resources["DepositModeBackgroundConv"] = new DepositModeBackgroundConverter();
                app.Resources["VisibilityConv"] = new BooleanToVisibilityConverter();
                app.Resources["UIModeVisibilityConv"] = new UIModeToVisibilityConverter();
                app.Resources["BoolToVis"] = new BooleanToVisibilityConverter();
                app.Resources["StringNullOrEmptyToVisibilityConv"] = new StringNullOrEmptyToVisibilityConverter();
                app.Resources["IntToStrConv"] = new IntToStringConverter();
                
                // Static strings and brushes if needed
                app.Resources["ValidationError"] = "VAL ERROR";
                app.Resources["SuccessBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
                app.Resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));

                // Instantiate Dependencies
                var mockInv = new Mock<Inventory>();
                mockInv.Setup(i => i.Changed).Returns(Observable.Empty<DenominationKey>());
                
                var mockHistory = new Mock<TransactionHistory>();
                mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());
                
                var config = new ConfigurationProvider();
                config.Config.System.CurrencyCode = "JPY";
                var metadata = new CurrencyMetadataProvider(config);
                var monitors = new MonitorsProvider(mockInv.Object, config, metadata);
                var aggregator = new OverallStatusAggregator(monitors.Monitors);
                
                var hardware = new HardwareStatusManager();
                var depositController = new DepositController(mockInv.Object, hardware);
                var mockSimulator = new Mock<IDeviceSimulator>();
                var dispenseController = new DispenseController(new Mock<CashChangerManager>(mockInv.Object, mockHistory.Object, new ChangeCalculator()).Object, hardware, mockSimulator.Object);
                var deps = new SimulatorDependencies { Inventory = mockInv.Object, History = mockHistory.Object };
                var mockChanger = new Mock<SimulatorCashChanger>(deps);
                var mockNotify = new Mock<INotifyService>();

                var facade = new DeviceFacade(
                    mockInv.Object,
                    new Mock<CashChangerManager>(mockInv.Object, mockHistory.Object, new ChangeCalculator()).Object,
                    depositController,
                    dispenseController,
                    hardware,
                    mockChanger.Object,
                    mockHistory.Object,
                    new OverallStatusAggregatorProvider(monitors),
                    monitors,
                    mockNotify.Object,
                    new ImmediateDispatcherService(),
                    new Mock<IViewService>().Object);

                var invVM = new InventoryViewModel(
                    facade,
                    config,
                    metadata,
                    new Mock<IHistoryExportService>().Object,
                    mockNotify.Object);

                // Test ActivityFeedControl
                var activityFeed = new ActivityFeedControl { DataContext = invVM };
                activityFeed.ShouldNotBeNull();

                // Test InventoryControl
                var inventoryControl = new InventoryControl { DataContext = invVM };
                inventoryControl.ShouldNotBeNull();

                var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                services.AddSingleton<IDeviceFacade>(facade);
                services.AddSingleton(config);
                services.AddSingleton(metadata);
                services.AddSingleton<IViewModelFactory, ViewModelFactory>();
                services.AddSingleton(new Mock<IHistoryExportService>().Object);
                services.AddSingleton(new Mock<CashChangerSimulator.Device.Services.IScriptExecutionService>().Object);
                services.AddSingleton(facade.Notify);
                services.AddSingleton<IDepositOperationService>(new Mock<IDepositOperationService>().Object);
                services.AddSingleton<IDispenseOperationService>(new Mock<IDispenseOperationService>().Object);
                services.AddSingleton<InventoryViewModel>();
                services.AddSingleton<AdvancedSimulationViewModel>();
                services.AddSingleton<MainViewModel>();
                var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IViewModelFactory>();

                var mainVM = new Mock<MainViewModel>(
                    factory,
                    facade,
                    config,
                    metadata,
                    mockNotify.Object,
                    provider.GetRequiredService<CashChangerSimulator.Device.Services.IScriptExecutionService>()
                );

                // Initialize DIContainer with our mock provider
                DIContainer.SetProvider(provider);

                var mainWindow = new MainWindow();
                mainWindow.DataContext = mainVM.Object;
                mainWindow.ShouldNotBeNull();
                
                // Explicitly close window
                mainWindow.Close();
            }
            catch (Exception ex)
            {
                Assert.Fail($"XAML Load Failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // We avoid Application.Current.Shutdown() because it can affect the host process in certain environments.
                // Instead, we rely on mainWindow.Close() inside the try block.
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        // Wait for a reasonable timeout
        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            thread.Interrupt();
            Assert.Fail("UISmokeTest timed out (possible deadlock or long XAML load).");
        }
    }
}
