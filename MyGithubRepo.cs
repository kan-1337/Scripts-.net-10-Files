#:package Flurl.Http@4.0.2
#:property PublishAot=false
using Flurl.Http;
using System.Text.Json;

// Get PUBLIC user info for kan86
var user = await "https://api.github.com/users/kan86"
    .WithHeader("Accept", "application/vnd.github.v3+json")
    .WithHeader("User-Agent", "dotnet-script")
    .GetJsonAsync<GitHubUser>();

Console.WriteLine($"Name: {user.name}");
Console.WriteLine($"Login: {user.login}");
Console.WriteLine($"Public Repos: {user.public_repos}");
Console.WriteLine($"Followers: {user.followers}");
Console.WriteLine($"Bio: {user.bio}");
Console.WriteLine($"Location: {user.location}");
Console.WriteLine($"Company: {user.company}");

record GitHubUser(
    string login,
    string? name,
    string? bio,
    string? location,
    string? company,
    int public_repos,
    int followers,
    int following,
    string avatar_url,
    string html_url
);