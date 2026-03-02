# CLI 操作説明書 (CLI Operating Instructions)

CLI UI は、釣銭機シミュレーターをコマンドラインから操作・管理するためのインターフェースです。自動テストやスクリプトによる一括操作に最適化されています。

## 1. 起動方法

リポジトリのルートディレクトリから以下のコマンドで起動できます。

```powershell
dotnet run --project src/CashChangerSimulator.UI.Cli
```

### 起動オプション
- `--currency <CODE>`: 指定した通貨コード（例: `USD`）で構成を上書きして起動します。

## 2. コマンド一覧

起動後、以下のコマンドを入力して操作します。

| コマンド            | 説明                                                                             |
| :------------------ | :------------------------------------------------------------------------------- |
| `status`            | デバイスの状態（State）と現在の在庫（Inventory）の概要を表示します。             |
| `open`              | デバイスをオープン（初期化）します。                                             |
| `claim [timeout]`   | デバイスの排他的アクセス権を取得します。デフォルトのタイムアウトは 1000ms です。 |
| `enable`            | デバイスを使用可能状態（Enabled）にします。                                      |
| `readCashCounts`    | デバイスから詳細な在高情報を読み取り、表形式で表示します。                       |
| `deposit <amount>`  | 指定した金額の入金処理をシミュレートします。                                     |
| `dispense <amount>` | 指定した金額の払出処理を実行します。                                             |
| `history [count]`   | 最近の取引履歴を表示します（デフォルト 10 件）。                                 |
| `run-script <path>` | 指定した JSON ファイルに基づき、一連の操作を自動実行します。                     |
| `disable`           | デバイスを無効化します。                                                         |
| `release`           | 排他的アクセス権を解放します。                                                   |
| `close`             | デバイスをクローズします。                                                       |
| `exit` / `quit`     | CLI アプリケーションを終了します。                                               |

## 3. シナリオ実行 (run-script)

JSON 形式のスクリプトファイルを作成することで、複雑な操作を自動化できます。

### スクリプト例 (`sample.json`)

```json
[
  { "Op": "BeginDeposit" },
  { "Op": "TrackDeposit", "Currency": "JPY", "Type": "Bill", "Value": 1000, "Count": 1 },
  { "Op": "FixDeposit" },
  { "Op": "EndDeposit", "Action": "Store" },
  { "Op": "Delay", "Value": 500 },
  { "Op": "Dispense", "Value": 1000 }
]
```

### 実行コマンド
```text
> run-script sample.json
```

## 4. ログ

CLI の詳細な動作ログは、アプリケーション実行ディレクトリの `logs/` フォルダに出力されます（WPF UI と共通）。
