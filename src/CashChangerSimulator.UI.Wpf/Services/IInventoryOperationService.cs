namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>在庫操作およびデバイスの基本操作の実行ロジックを担当するサービスのインターフェース。</summary>
public interface IInventoryOperationService
{
    /// <summary>デバイスをオープンします。</summary>
    void OpenDevice();

    /// <summary>デバイスをクローズします。</summary>
    void CloseDevice();

    /// <summary>デバイスのエラーをリセットします。</summary>
    void ResetError();

    /// <summary>全在庫の回収を実行します（在庫をすべて 0 にします）。</summary>
    void CollectAll();

    /// <summary>全在庫の補充を実行します（各金種を設定された初期枚数にリセットします）。</summary>
    void ReplenishAll();

    /// <summary>取引履歴をエクスポートします。</summary>
    void ExportHistory();

    /// <summary>ジャムエラーの状態をシミュレートします。</summary>
    void SimulateJam();

    /// <summary>重なりエラーの状態をシミュレートします。</summary>
    void SimulateOverlap();

    /// <summary>デバイスエラーの状態をシミュレートします。</summary>
    void SimulateDeviceError();
}
