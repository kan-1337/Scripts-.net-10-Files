#:package Flurl.Http@4.0.2
#:package ClosedXML@0.104.2
#:property PublishAot=false

using Flurl;
using Flurl.Http;
using System.Text.Json;
using ClosedXML.Excel;

var credentialsPath = @"C:\Repositories\amadeusapi.json";

if (!File.Exists(credentialsPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: Credentials file not found at: {credentialsPath}");
    Console.ResetColor();
    return;
}

var credentialsJson = await File.ReadAllTextAsync(credentialsPath);
var credentials = JsonSerializer.Deserialize<ApiCredentials>(credentialsJson)
                  ?? throw new InvalidOperationException("Could not deserialize credentials.");

var clientId = credentials.apikey;
var clientSecret = credentials.apiSecret;
var tpToken = credentials.aviasales;

if (string.IsNullOrWhiteSpace(tpToken))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: Aviasales token missing");
    Console.ResetColor();
    return;
}

var from = "CPH";
var to = "NRT";
var currency = "DKK";
var maxPrice = 8000m;
var maxStops = 1;

Console.WriteLine("Flight Search: CPH -> Tokyo/Narita (2026)");
Console.WriteLine(new string('=', 100) + "\n");

var token = await GetAccessToken(clientId, clientSecret);

var searchPeriods = new[]
{
    (Month: "May 2026", Start: "2026-05-01"),
    (Month: "June 2026", Start: "2026-06-01"),
    (Month: "July 2026", Start: "2026-07-01"),
    (Month: "August 2026", Start: "2026-08-01")
};

var allFlights = new List<FlightOffer>();

foreach (var period in searchPeriods)
{
    Console.WriteLine($"[Amadeus] Searching {period.Month}...");
    try
    {
        var response = await "https://test.api.amadeus.com/v2/shopping/flight-offers"
            .WithOAuthBearerToken(token)
            .SetQueryParams(new
            {
                originLocationCode = from,
                destinationLocationCode = to,
                departureDate = period.Start,
                returnDate = DateTime.Parse(period.Start).AddDays(14).ToString("yyyy-MM-dd"),
                adults = 1,
                currencyCode = currency,
                max = 50
            })
            .GetStringAsync();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<AmadeusResponse>(response, options);

        if (data?.data != null)
        {
            allFlights.AddRange(data.data);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Found {data.data.Count} flights\n");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  No flights found\n");
            Console.ResetColor();
        }

        await Task.Delay(2000);
    }
    catch (FlurlHttpException ex)
    {
        var err = await ex.GetResponseStringAsync();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.StatusCode}\n{err}\n");
        Console.ResetColor();
    }
}

if (allFlights.Count == 0)
{
    Console.WriteLine("NO FLIGHTS FOUND.");
    return;
}

var filteredFlights = allFlights
    .Where(f => f.itineraries.All(i => i.segments.Count - 1 <= maxStops))
    .OrderBy(f => decimal.Parse(f.price.total))
    .ToList();

var workbook = new XLWorkbook();
var sheet = workbook.Worksheets.Add("Tokyo Flights 2026");

sheet.Cell(1, 1).Value = "Rank";
sheet.Cell(1, 2).Value = "Departure";
sheet.Cell(1, 3).Value = "Departure Time";
sheet.Cell(1, 4).Value = "Return";
sheet.Cell(1, 5).Value = "Return Time";
sheet.Cell(1, 6).Value = "Price (DKK)";
sheet.Cell(1, 7).Value = "Stops (Out)";
sheet.Cell(1, 8).Value = "Stops (Ret)";
sheet.Cell(1, 9).Value = "Duration";
sheet.Cell(1, 10).Value = "Airlines";
sheet.Cell(1, 11).Value = "Deal Quality";

var header = sheet.Range(1, 1, 1, 11);
header.Style.Font.Bold = true;
header.Style.Fill.BackgroundColor = XLColor.DarkBlue;
header.Style.Font.FontColor = XLColor.White;

int r = 2;
int k = 1;

foreach (var flight in filteredFlights)
{
    var price = decimal.Parse(flight.price.total);
    var outIt = flight.itineraries[0];
    var retIt = flight.itineraries.Count > 1 ? flight.itineraries[1] : null;

    var depDate = DateTime.Parse(outIt.segments[0].departure.at);
    var retDate = retIt != null
        ? DateTime.Parse(retIt.segments.Last().arrival.at)
        : depDate;

    var airlines = string.Join(", ", outIt.segments.Select(s => s.carrierCode).Distinct());

    sheet.Cell(r, 1).Value = k;
    sheet.Cell(r, 2).Value = depDate.ToString("dd MMM yyyy");
    sheet.Cell(r, 3).Value = depDate.ToString("HH:mm");
    sheet.Cell(r, 4).Value = retDate.ToString("dd MMM yyyy");
    sheet.Cell(r, 5).Value = retDate.ToString("HH:mm");
    sheet.Cell(r, 6).Value = price;
    sheet.Cell(r, 7).Value = outIt.segments.Count - 1;
    sheet.Cell(r, 8).Value = retIt?.segments.Count - 1 ?? 0;
    sheet.Cell(r, 9).Value = FormatIsoDuration(outIt.duration);
    sheet.Cell(r, 10).Value = airlines;

    var deal = price <= maxPrice ? "GREAT DEAL!" :
               price <= maxPrice * 1.2m ? "Good" :
               price <= maxPrice * 1.5m ? "OK" : "Expensive";

    sheet.Cell(r, 11).Value = deal;

    if (price <= maxPrice) sheet.Range(r, 1, r, 11).Style.Fill.BackgroundColor = XLColor.LightGreen;
    if (price > maxPrice * 1.5m) sheet.Range(r, 1, r, 11).Style.Fill.BackgroundColor = XLColor.LightPink;

    Console.WriteLine($"{k}. {depDate:dd MMM yyyy HH:mm} -> {retDate:dd MMM yyyy HH:mm} | {price:N0} DKK | {airlines} | {FormatIsoDuration(outIt.duration)}");

    k++;
    r++;
}

sheet.Columns().AdjustToContents();

var excelPath = @"C:\Repositories\TokyoFlights_2026.xlsx";
workbook.SaveAs(excelPath);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\nExcel saved: {excelPath}");
Console.ResetColor();

Console.WriteLine("\n" + new string('=', 100));
Console.WriteLine("[Travelpayouts] Querying...");

var tpOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

foreach (var p in searchPeriods)
{
    var ym = DateTime.Parse(p.Start).ToString("yyyy-MM");
    Console.WriteLine($"[TP] {p.Month} ({ym})");

    try
    {
        var json = await "https://api.travelpayouts.com/aviasales/v3/prices_for_dates"
            .SetQueryParams(new
            {
                origin = from,
                destination = to,
                departure_at = ym,
                return_at = ym,
                unique = "false",
                sorting = "price",
                direct = "false",
                cy = "dkk",
                limit = 100,
                page = 1,
                one_way = "false",
                token = tpToken
            })
            .GetStringAsync();

        var resp = JsonSerializer.Deserialize<TPResponse>(json, tpOptions);

        if (resp?.data != null && resp.data.Count > 0)
        {
            var cheapest = resp.data.OrderBy(f => f.price).First();
            var mins = cheapest.duration;
            var hours = mins / 60;
            var m = mins % 60;

            var rub = cheapest.price;
            var dkk = Math.Round(rub * 0.089m, 0);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                $"  {cheapest.departure_at} -> {cheapest.return_at} | {dkk:N0} DKK (~{rub:N0} RUB) | {hours}h {m}m | {cheapest.airline} | stops: {cheapest.transfers}\n");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine("  No TP data\n");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}\n");
    }
}

Console.WriteLine(new string('=', 100));
Console.WriteLine("[Done]");

static async Task<string> GetAccessToken(string clientId, string clientSecret)
{
    var response = await "https://test.api.amadeus.com/v1/security/oauth2/token"
        .PostUrlEncodedAsync(new
        {
            grant_type = "client_credentials",
            client_id = clientId,
            client_secret = clientSecret
        })
        .ReceiveJson<TokenResponse>();

    return response.access_token;
}

static string FormatIsoDuration(string iso)
{
    if (string.IsNullOrWhiteSpace(iso)) return "";
    int hours = 0, minutes = 0;
    var s = iso.StartsWith("PT") ? iso[2..] : iso;
    var hIdx = s.IndexOf('H');
    if (hIdx >= 0)
    {
        int.TryParse(s[..hIdx], out hours);
        s = s[(hIdx + 1)..];
    }
    var mIdx = s.IndexOf('M');
    if (mIdx >= 0) int.TryParse(s[..mIdx], out minutes);
    return $"{hours}h {minutes}m";
}

record ApiCredentials(string apikey, string apiSecret, string aviasales);
record TokenResponse(string access_token);
record AmadeusResponse(List<FlightOffer> data);
record FlightOffer(Price price, List<Itinerary> itineraries);
record Price(string total);
record Itinerary(string duration, List<Segment> segments);
record Segment(Departure departure, Arrival arrival, string carrierCode);
record Departure(string at);
record Arrival(string at);

record TPResponse(List<TPFlight> data);
record TPFlight(
    string origin,
    string destination,
    decimal price,
    string airline,
    string flight_number,
    string departure_at,
    string return_at,
    int transfers,
    int duration
);
