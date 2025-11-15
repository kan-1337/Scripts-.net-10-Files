#:package Flurl.Http@4.0.2
#:property PublishAot=false
using Flurl;
using Flurl.Http;
using System.Text.Json;

Console.WriteLine("💰 Cryptocurrency Price Monitor\n");
Console.WriteLine(new string('=', 80) + "\n");

var currencies = new[] { "usd", "eur", "dkk", "sek", "nok" };
var cryptos = new[] { "bitcoin", "ethereum", "solana" };

var targetPrices = new Dictionary<string, decimal?>
{
    { "bitcoin-dkk", 500000m },
    { "bitcoin-usd", null },
    { "ethereum-dkk", null }
};

var refreshInterval = TimeSpan.FromHours(1);

while (true)
{
    try
    {
        foreach (var crypto in cryptos)
        {
            var response = await "https://api.coingecko.com/api/v3/simple/price"
                .SetQueryParams(new
                {
                    ids = crypto,
                    vs_currencies = string.Join(",", currencies),
                    include_24hr_change = "true"
                })
                .GetStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(response, options);

            if (data != null && data.ContainsKey(crypto))
            {
                var prices = data[crypto];

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"📊 {char.ToUpper(crypto[0]) + crypto.Substring(1)}");
                Console.ResetColor();

                foreach (var currency in currencies)
                {
                    if (prices.TryGetValue(currency, out var priceElement))
                    {
                        var price = priceElement.GetDecimal();
                        var changeKey = $"{currency}_24h_change";
                        var change = prices.TryGetValue(changeKey, out var changeElement)
                            ? changeElement.GetDecimal()
                            : 0m;

                        var symbol = currency.ToUpper() switch
                        {
                            "USD" => "$",
                            "EUR" => "€",
                            "DKK" => "kr",
                            "SEK" => "kr",
                            "NOK" => "kr",
                            _ => currency.ToUpper()
                        };

                        var formattedPrice = price >= 1000
                            ? $"{price:N0}"
                            : $"{price:N2}";

                        Console.Write($"  {currency.ToUpper()}: {symbol}{formattedPrice} ");

                        if (change >= 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write($"▲ +{change:F2}%");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write($"▼ {change:F2}%");
                        }
                        Console.ResetColor();

                        var key = $"{crypto}-{currency}";
                        if (targetPrices.TryGetValue(key, out var target) && target.HasValue)
                        {
                            if (price <= target.Value)
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.Write($"  TARGET HIT! (≤{symbol}{target.Value:N0})");
                                Console.ResetColor();
                            }
                        }

                        Console.WriteLine();
                    }
                }
                Console.WriteLine();
            }
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Last updated: {DateTime.Now:HH:mm:ss}");
        Console.WriteLine($"Next refresh in {refreshInterval.TotalMinutes} minute(s)... (Press Ctrl+C to exit)");
        Console.ResetColor();
        Console.WriteLine(new string('=', 80) + "\n");

        await Task.Delay(refreshInterval);
        Console.Clear();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Error: {ex.Message}");
        Console.ResetColor();
        await Task.Delay(5000);
    }
}