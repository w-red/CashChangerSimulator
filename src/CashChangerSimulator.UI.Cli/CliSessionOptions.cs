namespace CashChangerSimulator.UI.Cli;

public class CliSessionOptions
{
    public bool IsAsync { get; set; }
    public bool Verbose { get; set; }
    public string Language { get; set; } = "en";
    public string CurrencyCode { get; set; } = "JPY";
}
