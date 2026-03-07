# C# 14: Extension Members (拡張メンバ) 調査資料

## 概要
C# 14 では、従来の拡張メソッドをさらに進化させた「拡張メンバ」が導入されます。これにより、メソッドだけでなくプロパティ、インデックス、静的メンバなどを既存の型に追加できるようになります。

## 主な特徴
- **プロパティの拡張**: メソッドだけでなく `get`/`set` アクセサを持つプロパティを拡張タイプとして定義可能。
- **静的な拡張**: インスタンスメンバだけでなく、型自体に対する静的な拡張メンバも定義可能。
- **カプセル化の維持**: 既存のオブジェクトの状態を破壊することなく、インターフェースやクラスに新しい「外見（View）」を提供できる。

## 構文例
```csharp
public extension ListExtensions for List<T>
{
    public int LastIndex => this.Count - 1;
    
    public static void DefaultAction() => Console.WriteLine("Default");
}
```

## プロジェクトへの適用案
- **SimulatorCashChanger の軽量化**: 現在 `SimulatorCashChanger` にある UI 専用プロパティやデバッグ用メンバを、拡張メンバとして別ファイルに抽出することで、サービスオブジェクトとしての責務（UPOS準拠）のみを本体に残すことが可能になります。
- **流れるようなインターフェース (Fluent API)**: デバイス操作に対して、より直感的で読みやすいプロパティベースの拡張を提供できます。

## 参考資料
- [What's new in C# 14 - Extension members](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14#extension-members)
