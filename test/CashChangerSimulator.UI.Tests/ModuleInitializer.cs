using System.Runtime.CompilerServices;
using VerifyTests;

namespace CashChangerSimulator.UI.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyImageMagick.Initialize();
        VerifyImageMagick.RegisterComparers(0.25); // Global 25% threshold
    }
}
