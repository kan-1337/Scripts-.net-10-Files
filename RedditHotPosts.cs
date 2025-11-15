#:package Flurl.Http@4.0.2
#:property PublishAot=false
using Flurl;
using Flurl.Http;
using System.Text.Json;

var subreddit = "programming";
Console.WriteLine($"Fetching top posts from r/{subreddit}...\n");
Console.WriteLine(new string('=', 80) + "\n");

var response = await $"https://www.reddit.com/r/{subreddit}/hot.json"
    .WithHeader("User-Agent", "dotnet-script/1.0")
    .SetQueryParams(new { limit = 10 })
    .GetStringAsync();

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var data = JsonSerializer.Deserialize<RedditResponse>(response, options);

if (data?.data?.children != null)
{
    int rank = 1;
    foreach (var post in data.data.children)
    {
        var p = post.data;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"{rank}. {p.title}");
        Console.ResetColor();

        Console.WriteLine($"   r/{p.subreddit} | Score: {p.score} | Comments: {p.num_comments}");
        Console.WriteLine($"   {p.url}");

        if (!string.IsNullOrWhiteSpace(p.selftext) && p.selftext.Length > 10)
        {
            var summary = p.selftext.Length > 200
                ? p.selftext.Substring(0, 200) + "..."
                : p.selftext;
            Console.WriteLine($"\n   Summary: {summary}");
        }

        var topComment = await GetTopComment(p.permalink);
        if (!string.IsNullOrEmpty(topComment))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n   Top Comment: {topComment}");
            Console.ResetColor();
        }

        Console.WriteLine("\n" + new string('-', 80) + "\n");
        rank++;
    }
}

static async Task<string> GetTopComment(string permalink)
{
    try
    {
        var commentsUrl = $"https://www.reddit.com{permalink}.json";
        var response = await commentsUrl
            .WithHeader("User-Agent", "dotnet-script/1.0")
            .WithTimeout(5)
            .GetStringAsync();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var commentData = JsonSerializer.Deserialize<List<RedditCommentResponse>>(response, options);

        if (commentData != null && commentData.Count > 1)
        {
            var topComment = commentData[1]?.data?.children?.FirstOrDefault()?.data?.body;

            if (!string.IsNullOrWhiteSpace(topComment))
            {
                return topComment.Length > 300
                    ? topComment.Substring(0, 300) + "..."
                    : topComment;
            }
        }

        return string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}

record RedditResponse(RedditData data);
record RedditData(List<RedditChild> children);
record RedditChild(RedditPost data);
record RedditPost(
    string title,
    int score,
    int num_comments,
    string url,
    string subreddit,
    string selftext,
    string permalink
);

record RedditCommentResponse(RedditCommentData data);
record RedditCommentData(List<RedditCommentChild> children);
record RedditCommentChild(RedditComment data);
record RedditComment(string body);