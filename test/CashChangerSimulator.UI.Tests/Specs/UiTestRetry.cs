using System;
using FlaUI.Core.AutomationElements;

namespace CashChangerSimulator.UI.Tests.Specs;

public static class UiTestRetry
{
    public static AutomationElement Find(Func<AutomationElement?> findFunc, TimeSpan timeout)
    {
        AutomationElement? result = null;
        FlaUI.Core.Tools.Retry.WhileTrue(() => {
            result = findFunc();
            return result == null;
        }, timeout);
        return result!;
    }
}
