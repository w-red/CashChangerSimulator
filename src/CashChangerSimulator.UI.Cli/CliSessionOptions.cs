namespace CashChangerSimulator.UI.Cli;

public class CliSessionOptions
{
    public bool IsAsync { get; set; }
    public string Language { get; set; } = "ja";
    public string CurrencyCode { get; set; } = "JPY";
}
