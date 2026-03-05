# WPF UIテストにおける FlaUI と DataBinding のベストプラクティスと教訓

本ドキュメントでは、CashChangerSimulator の UI テスト実装における課題解決から得られた、WPF と FlaUI を組み合わせた際の重要な教訓を記録します。

## 1. ValuePattern による直接代入と DataBinding の問題

WPF の `TextBox` に対して FlaUI の `ValuePattern.SetValue` やプロパティへの直接代入（`firstTextBox.Text = quantity`）を使用した場合、**UI上の表示は更新されても、背後の ViewModel に値が伝播しない（`UpdateSourceTrigger=PropertyChanged` が発火しない）** という問題が発生します。

### 解決策
WPFのバインディングメカニズムを確実にトリガーするためには、人間の入力と同じようにキーコードをエミュレートする必要があります。
```csharp
// 悪い例 (バインディングが発火しない場合がある)
firstTextBox.Text = "2";

// 良い例 (確実なキーボード入力シミュレーション)
firstTextBox.Focus();
Thread.Sleep(100);
FlaUI.Core.Input.Keyboard.Type("2");
Thread.Sleep(100);
FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
Thread.Sleep(500); // INotifyPropertyChangedの伝播を待つ
```

## 2. IsEnabled のハードコードと Command.CanExecute の競合

XAML 上で `IsEnabled="{Binding SomeProperty}"` を直接指定してしまうと、その要素にバインドされた `ICommand` (例: ReactiveCommand) の `CanExecute` ロジックが意図せず上書きされたり、干渉したりすることがあります。
特に「特定モード中は全体として無効（CanOperate = false）」といったグローバルなプロパティを、個別のユースケースを持つボタン（例：入金中の「終了」や「ストア」ボタン）に一律に適用すると、正しく機能しなくなるバグの温床になります。

### 解決策
ボタンの有効/無効の制御は **ViewModel 内の `Command.CanExecute` に集約** し、XAML 側で `IsEnabled` をハードコードするのを避けるべきです。
```xml
<!-- 悪い例 -->
<Button Command="{Binding StoreDepositCommand}" IsEnabled="{Binding CanOperate.Value}" />

<!-- 良い例 (ViewModelの StoreDepositCommand.CanExecute に完全に委ねる) -->
<Button Command="{Binding StoreDepositCommand}" />
```

## 3. FindElement と AutomationId の null 渡しによる例外

FlaUI の `FindFirstDescendant` に対して、`ByAutomationId(null)` や `ByText(null)` のような条件式を渡すと、COMの階層で `System.ArgumentException` が発生しテストがクラッシュします。

### 解決策
カスタムヘルパーメソッド内で Condition を生成する際は、事前に文字列が null または 空文字 でないことを必ずチェックする必要があります。
```csharp
if (!string.IsNullOrEmpty(automationId))
{
    var el = container.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    if (el != null) return el;
}
```

これらの教訓は、WPFアプリケーションをFlaUI等のUI Automationフレームワークで自動テストする際の安定性向上に不可欠です。
