# ビルド警告の解決ガイド (Build Warning Resolution Guide)

このドキュメントでは、CashChangerSimulator プロジェクトで発生した一般的なビルド警告の理由とその解決方法についてまとめています。

## C# 静的解析 (Null 許容性関連)

### CS8602: null 参照の可能性があるものの逆参照
- **発生理由**: Null 許容参照型が有効な環境で、null である可能性がある変数に対してメンバーアクセスを行っている場合に発生します。
- **解決策**:
    - **Null 条件演算子 (`?.`)**: `object?.Property`
    - **Null 合体演算子 (`??`)**: `var x = object ?? throw new Exception(...)`
    - **明示的な検証**: `if (object == null) return;`
- **例**:
  ```csharp
  // 修正前
  var window = Application.Current.MainWindow; 
  // 修正後
  var app = Application.Current;
  if (app != null) { var window = app.MainWindow; }
  ```

### CS8603: Null 参照戻り値である可能性があります
- **発生理由**: 戻り値の型が非 null (`T`) として宣言されているメソッドから `null` を返そうとしています。
- **解決策**: 戻り値の型を Null 許容型 (`T?`) に変更するか、代替の非 null 値を返します。

### CS8633 / CS8767: インターフェース実装の Null 許容性の不一致
- **発生理由**: 実装しているインターフェースのメソッド定義と、実装クラス側の引数や型制約の Null 許容性についての属性が一致していません。
- **解決策**: インターフェースのシグネチャ（`T?` や `where TState : notnull` 等）を正確に一致させます。

---

## xUnit.net 推奨事項

### xUnit1051: CancellationToken の使用
- **発生理由**: 非同期メソッド（`Task.Delay` 等）の呼び出し時に `CancellationToken` を渡していないため、テストの中断に対する応答性が低い場合に発生します。
- **解決策**: `TestContext.Current.CancellationToken` を渡すようにします。
- **例**:
  ```csharp
  // 修正前
  await Task.Delay(100);
  // 修正後
  await Task.Delay(100, TestContext.Current.CancellationToken);
  ```

---

## MSBuild / ファイルロック関連

### MSB3026 / MSB3061: ファイルをコピーできない (プロセスによるロック)
- **発生理由**: 以前のビルドやユニットテストで実行されたプロセス（WPF アプリ、テストランナー等）が終了せず、DLL ファイルを掴み続けている場合に発生します。
- **解決策**:
    - **テスト終了処理の強化**: テスト内で生成した Window を `Close()` し、スレッドを確実に `Join()` させて終了を待ちます。
    - **プロセスの手動終了**: `taskkill /F /IM <ProcessName> /T` で古いプロセスを強制終了します。
    - **タイムアウトの導入**: スレッドの待機にタイムアウトを設定し、ゾンビプロセスの発生を防ぎます。

---

## ナレッジの適用
新しいコードを追加する際は、これらの警告が発生しないように最初から設計に組み込むことを推奨します。特に **XAML スモークテスト** や **静的チェッカー** と組み合わせることで、実行時エラーと警告の両方を最小限に抑えることができます。
