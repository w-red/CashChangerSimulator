# 標準モード操作説明書 (Application Operating Instructions)

標準モードは、デバイス単体としての機能をGUIから直接操作・確認するためのモードです。

## 1. 画面構成

- **Inventory 画面**: 現在の在庫（金種ごとの枚数）が表示されます。
- **Deposit/Dispense 画面**: 手動での入金および払出シミュレーションを行います。
- **Activity Feed**: デバイス内で発生したイベント（DataEvent, StatusUpdateEvent等）が時系列で表示されます。

![メインダッシュボード画面](images/main_dashboard.png)
*図：シミュレーターのメインダッシュボード画面*

## 2. 在庫の調整 (Inventory Management)

1. **初期値の設定**: アプリ起動時、`config.toml` に定義された内容で在庫が初期化されます。
2. **手動更新**: Inventory タブから各金種の枚数を直接編集し、「Save」または「Apply」をクリックすることで、シミュレーター内部の在庫を即座に更新できます。
3. **Discrepancy (不一致) 設定**: シミュレーションメニューから「Set Discrepancy」を有効にすると、インベントリ情報の不一致状態をシミュレートできます。

## 3. 手動入金操作 (Deposit)

![入金ウィンドウ](images/deposit_window.png)

1. `Deposit` タブを選択します。
2. 「Begin Deposit」ボタンをクリックします。
3. 枚数を選択または入力し、「Insert Cash」をクリックして現金を投入します。
4. 「End Deposit」をクリックすると、入金が確定し、必要に応じて釣銭が払い出されます。

## 4. 手動払出操作 (Dispense)

1. `Dispense` タブを選択します。
2. **金額指定払出 (`DispenseChange`)**: 払出したい合計金額を入力し、「Execute」をクリックします。最適な金種の組み合わせが自動計算されます。
3. **金種指定払出 (`DispenseCash`)**: 各金種の枚数を指定し、「Dispense Cash」をクリックします。

## 5. ログの確認

- リアルタイムな挙動は **Activity Feed** で確認可能です。
- 詳細なデバッグログは、アプリケーション実行ディレクトリの `logs/` フォルダ内に出力されます。

## 6. 設定ファイル (config.toml)

アプリケーション実行ディレクトリの `config.toml` を編集することで、シミュレーターの動作をカスタマイズできます。アプリ初回起動時にデフォルト値で自動生成されます。

### `[System]` — システム全般

| キー           | 型     | デフォルト   | 説明                                      |
| -------------- | ------ | ------------ | ----------------------------------------- |
| `CurrencyCode` | string | `"JPY"`      | 使用する通貨コード（ISO 4217）            |
| `CultureCode`  | string | `"en-US"`    | UIのロケール設定                          |
| `UIMode`       | string | `"Standard"` | UIモード（`Standard` / `PosTransaction`） |

### `[Inventory.<通貨コード>.Denominations.<金種キー>]` — 在庫設定

通貨ごとの金種別初期設定。金種キーは `B` (紙幣) または `C` (硬貨) + 額面で構成されます（例: `B1000`, `C100`）。

| キー           | 型     | デフォルト | 説明                                   |
| -------------- | ------ | ---------- | -------------------------------------- |
| `DisplayName`  | string | —          | UIに表示される金種名（例: `"1000円"`） |
| `InitialCount` | int    | `0`        | 起動時の初期保有枚数                   |
| `NearEmpty`    | int    | `5`        | NearEmpty 判定しきい値（この枚数以下） |
| `NearFull`     | int    | `90`       | NearFull 判定しきい値（この枚数以上）  |
| `Full`         | int    | `100`      | Full 判定しきい値（この枚数以上）      |

### `[Thresholds]` — デフォルトしきい値

金種個別設定がない場合に適用されるグローバルしきい値。

| キー        | 型  | デフォルト | 説明             |
| ----------- | --- | ---------- | ---------------- |
| `NearEmpty` | int | `5`        | NearEmpty 判定値 |
| `NearFull`  | int | `90`       | NearFull 判定値  |
| `Full`      | int | `100`      | Full 判定値      |

### `[Logging]` — ログ設定

| キー            | 型     | デフォルト      | 説明                                                     |
| --------------- | ------ | --------------- | -------------------------------------------------------- |
| `EnableConsole` | bool   | `true`          | コンソール出力の有効/無効                                |
| `EnableFile`    | bool   | `true`          | ファイル出力の有効/無効                                  |
| `LogLevel`      | string | `"Information"` | ログレベル（`Debug`, `Information`, `Warning`, `Error`） |
| `LogDirectory`  | string | `"logs"`        | ログ保存ディレクトリ                                     |
| `LogFileName`   | string | `"app.log"`     | ログファイル名                                           |

### `[Simulation]` — シミュレーション動作

| キー              | 型  | デフォルト | 説明                                    |
| ----------------- | --- | ---------- | --------------------------------------- |
| `DispenseDelayMs` | int | `500`      | 払い出し操作の遅延時間（ミリ秒、0以上） |

### 設定例

```toml
[System]
CurrencyCode = "JPY"
CultureCode = "ja-JP"
UIMode = "Standard"

[Inventory.JPY.Denominations.B1000]
DisplayName = "1000円"
InitialCount = 50
NearEmpty = 5
NearFull = 90
Full = 100

[Thresholds]
NearEmpty = 5
NearFull = 90
Full = 100

[Logging]
EnableConsole = true
EnableFile = true
LogLevel = "Information"
LogDirectory = "logs"
LogFileName = "app.log"

[Simulation]
DispenseDelayMs = 500
```

---
*英語版については、[ApplicationOperatingInstructions.md](ApplicationOperatingInstructions.md) を参照してください。*
