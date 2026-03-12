using Scalar.AspNetCore;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Core Domain Services
builder.Services.AddSingleton<Inventory>();
builder.Services.AddSingleton<TransactionHistory>();
builder.Services.AddSingleton<ChangeCalculator>();
builder.Services.AddSingleton<CashChangerManager>();
builder.Services.AddSingleton<HardwareStatusManager>();

// デバイス実装の DI 登録 (クラウド/テスト用は VirtualMockDevice を使用)
builder.Services.AddSingleton<ICashChangerDevice, VirtualMockDevice>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Content(@"
    <!DOCTYPE html>
    <html lang='ja'>
    <head>
        <meta charset='UTF-8'>
        <title>Virtual Cash Changer API</title>
        <style>
            body { 
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
                display: flex; 
                justify-content: center; 
                align-items: center; 
                height: 100vh; 
                margin: 0; 
                background: linear-gradient(135deg, #1a73e8 0%, #0d47a1 100%); 
                color: white;
            }
            .card { 
                background: rgba(255, 255, 255, 0.1); 
                backdrop-filter: blur(10px);
                padding: 3rem; 
                border-radius: 16px; 
                box-shadow: 0 10px 30px rgba(0,0,0,0.2); 
                text-align: center; 
                border: 1px solid rgba(255,255,255,0.2);
            }
            h1 { margin-bottom: 0.5rem; font-weight: 300; }
            p { opacity: 0.8; margin-bottom: 2rem; }
            a { 
                display: inline-block; 
                padding: 0.8rem 2rem; 
                background: white; 
                color: #1a73e8; 
                text-decoration: none; 
                border-radius: 30px; 
                font-weight: bold;
                transition: transform 0.2s, box-shadow 0.2s;
            }
            a:hover { 
                transform: translateY(-2px);
                box-shadow: 0 5px 15px rgba(0,0,0,0.3);
            }
        </style>
    </head>
    <body>
        <div class='card'>
            <h1>Virtual Cash Changer API is Running!</h1>
            <p>Cloud Run 上でシミュレータが正常に稼働しています。</p>
            <a href='/scalar/v1'>Scalar API ドキュメントを表示</a>
        </div>
    </body>
    </html>", "text/html"));

// --- Client API (POS Application) ---
var deviceApi = app.MapGroup("/api/device");

deviceApi.MapPost("/open", (ICashChangerDevice device) =>
{
    device.Open();
    return Results.Ok(new { status = "Opened" });
});

deviceApi.MapPost("/close", (ICashChangerDevice device) =>
{
    device.Close();
    return Results.Ok(new { status = "Closed" });
});

deviceApi.MapPost("/claim", (ICashChangerDevice device, int timeout) =>
{
    device.Claim(timeout);
    return Results.Ok(new { status = "Claimed" });
});

deviceApi.MapPost("/enable", (ICashChangerDevice device) =>
{
    device.Enable();
    return Results.Ok(new { status = "Enabled" });
});

app.MapGet("/api/inventory", (Inventory inventory) =>
{
    return Results.Ok(inventory.AllCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
});

app.MapPost("/api/dispense", (ICashChangerDevice device, decimal amount, string? currencyCode) =>
{
    try
    {
        device.Dispense(amount, currencyCode);
        return Results.Ok(new { status = "Dispensed", amount });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// --- Simulation API (for Testers) ---
var simulateApi = app.MapGroup("/api/simulate");

simulateApi.MapPost("/insert", (Inventory inventory, decimal value, string type, string currencyCode, int count) =>
{
    var cashType = Enum.TryParse<CurrencyCashType>(type, true, out var result) ? result : CurrencyCashType.Bill;
    var key = new DenominationKey(value, cashType, currencyCode);
    inventory.Add(key, count);
    return Results.Ok(new { status = "Inserted", key = key.ToString(), count });
});

simulateApi.MapPost("/error", (HardwareStatusManager status, bool error) =>
{
    if (error)
    {
        status.SetDeviceError(1, 0); // Example error
    }
    else
    {
        status.ResetError();
    }
    return Results.Ok(new { status = error ? "ErrorSet" : "ErrorCleared" });
});

app.Run();
