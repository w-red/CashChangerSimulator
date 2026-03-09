using System.Threading;
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
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.UI.Wpf.Converters;
using System.IO.Packaging;
using System.Windows.Controls;

namespace CashChangerSimulator.UI.Tests;

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
                var resources = Application.Current.Resources.MergedDictionaries;
                resources.Clear();
                
                // Essential styles and colors
                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/Colors.xaml") });
                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/Styles.xaml") });
                resources.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/Templates.xaml") });

                // Register standard converters used in XAML
                Application.Current.Resources["StatusBrushConv"] = new CashStatusToBrushConverter();
                Application.Current.Resources["TypeSymbolConv"] = new TransactionTypeToSymbolConverter();
                Application.Current.Resources["InvertedVisibilityConv"] = new InvertedBooleanToVisibilityConverter();
                Application.Current.Resources["VisibilityConv"] = new BooleanToVisibilityConverter();

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

                var invVM = new InventoryViewModel(
                    mockInv.Object, 
                    mockHistory.Object, 
                    aggregator, 
                    config, 
                    monitors, 
                    metadata, 
                    hardware, 
                    depositController, 
                    mockChanger.Object, 
                    mockNotify.Object);

                // Test ActivityFeedControl
                var activityFeed = new ActivityFeedControl { DataContext = invVM };
                activityFeed.ShouldNotBeNull();

                // Test InventoryControl
                var inventoryControl = new InventoryControl { DataContext = invVM };
                inventoryControl.ShouldNotBeNull();

                // Test MainWindow
                var mainVM = new Mock<MainViewModel>(
                    mockInv.Object, mockHistory.Object, new Mock<CashChangerManager>(mockInv.Object, mockHistory.Object, new ChangeCalculator()).Object,
                    monitors, new OverallStatusAggregatorProvider(monitors), config, metadata, hardware, depositController, dispenseController,
                    mockChanger.Object, mockNotify.Object, new Mock<CashChangerSimulator.Device.Services.IScriptExecutionService>().Object
                );
                var mainWindow = new MainWindow { DataContext = mainVM.Object };
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
                // Note: Shutting down the Application instance can break subsequent tests in the same process.
                // We'll let the test runner handle lifecycle if it's shared.
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
