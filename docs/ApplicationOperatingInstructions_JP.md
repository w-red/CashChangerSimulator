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

![入金ステータス表示](images/deposit_status.png)

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

---
*英語版については、[ApplicationOperatingInstructions.md](ApplicationOperatingInstructions.md) を参照してください。*
