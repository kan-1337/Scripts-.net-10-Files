#:package HtmlAgilityPack@1.11.71
#:package Flurl.Http@4.0.2
#:property PublishAot=false
using HtmlAgilityPack;
using Flurl.Http;
using System.Text;

Console.WriteLine("Fetching Hacker News top stories with summaries...\n");

var html = await "https://news.ycombinator.com"
    .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
    .GetStringAsync();

var doc = new HtmlDocument();
doc.LoadHtml(html);

var stories = doc.DocumentNode.SelectNodes("//tr[contains(@class,'athing')]");

if (stories != null && stories.Count > 0)
{
    Console.WriteLine($"Found {stories.Count} stories\n");
    Console.WriteLine(new string('=', 80) + "\n");

    int rank = 1;
    int pageSize = 10;
    int currentIndex = 0;

    while (currentIndex < stories.Count)
    {
        var batch = stories.Skip(currentIndex).Take(pageSize);

        foreach (var story in batch)
        {
            var titleLink = story.SelectSingleNode(".//span[contains(@class,'titleline')]//a");

            if (titleLink != null)
            {
                var title = titleLink.InnerText?.Trim();
                var url = titleLink.GetAttributeValue("href", "");

                Console.WriteLine($"{rank}. {title}");
                Console.WriteLine($"   🔗 {url}");

                var summary = await GetDetailedSummary(url);
                Console.WriteLine($"\n   📄 {summary}");
                Console.WriteLine("\n" + new string('-', 80) + "\n");

                rank++;
            }
        }

        currentIndex += pageSize;

        if (currentIndex < stories.Count)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"📊 Showing {currentIndex}/{stories.Count} stories");
            Console.WriteLine("Press any key for next 10 stories, or ESC to exit...");
            Console.ResetColor();

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine("\n👋 Exiting...");
                break;
            }

            Console.WriteLine("\n" + new string('=', 80) + "\n");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ All stories loaded!");
            Console.ResetColor();
        }
    }
}

/// <summary>
/// Fetches a detailed summary of a news article from the given URL.
/// </summary>
/// <param name="url">The URL of the news article.</param>
/// <returns>A task representing the asynchronous operation, with the article summary as the result.</returns>
static async Task<string> GetDetailedSummary(string url)
{
    try
    {
        var html = await url
            .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
            .WithTimeout(8)
            .GetStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var summary = new StringBuilder();

        // Try meta description first
        var metaDesc = doc.DocumentNode
            .SelectSingleNode("//meta[@name='description']")
            ?.GetAttributeValue("content", "");

        if (string.IsNullOrWhiteSpace(metaDesc))
        {
            metaDesc = doc.DocumentNode
                .SelectSingleNode("//meta[@property='og:description']")
                ?.GetAttributeValue("content", "");
        }

        if (!string.IsNullOrWhiteSpace(metaDesc))
        {
            summary.Append(metaDesc.Trim());
        }

        var paragraphs = doc.DocumentNode
            .SelectNodes("//p[string-length(normalize-space(text())) > 40]")
            ?.Take(3)
            .Select(p => System.Net.WebUtility.HtmlDecode(p.InnerText.Trim()))
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 40);

        if (paragraphs != null && paragraphs.Any())
        {
            if (summary.Length > 0)
            {
                summary.Append("\n\n   ");
            }

            var paraText = string.Join(" ", paragraphs);

            var maxLength = 500;
            if (paraText.Length > maxLength)
            {
                paraText = paraText.Substring(0, maxLength) + "...";
            }

            summary.Append(paraText);
        }

        return summary.Length > 0 ? summary.ToString() : "No summary available";
    }
    catch (Exception ex)
    {
        return $"Could not fetch summary ({ex.Message})";
    }
}