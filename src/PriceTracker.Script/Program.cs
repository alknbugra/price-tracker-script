using System.Globalization;
using System.Net.Http.Headers;
using CsvHelper;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Polly;
using Serilog;

Console.WriteLine("=== DEBUG START ===");
Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
Console.WriteLine($"appsettings.json exists: {File.Exists("appsettings.json")}");

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Console.WriteLine("=== CONFIG DEBUG ===");
Console.WriteLine($"Targets section exists: {configuration.GetSection("Targets").Exists()}");
Console.WriteLine($"Targets:Urls section exists: {configuration.GetSection("Targets:Urls").Exists()}");
Console.WriteLine($"Targets:Urls value: {configuration.GetSection("Targets:Urls").Value}");
Console.WriteLine($"Targets:Urls children count: {configuration.GetSection("Targets:Urls").GetChildren().Count()}");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
	var urls = configuration.GetSection("Targets:Urls").Get<string[]>() ?? Array.Empty<string>();
	Console.WriteLine($"URLs loaded: {urls.Length}");
	foreach (var url in urls) Console.WriteLine($"  - {url}");

    using var client = new HttpClient();
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PriceTrackerScript", "1.0"));

    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    var results = new List<PageRecord>();

    foreach (var url in urls)
    {
        try
        {
            Log.Information("Fetching {Url}", url);
            var html = await retryPolicy.ExecuteAsync(async () => await client.GetStringAsync(url));
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? string.Empty;

            results.Add(new PageRecord { Url = url, Title = title, RetrievedAt = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch {Url}", url);
        }
    }

    var outputPath = configuration["Output:CsvPath"] ?? "output.csv";
    await using var writer = new StreamWriter(outputPath);
    await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
    await csv.WriteRecordsAsync(results);

    Log.Information("Wrote {Count} records to {Path}", results.Count, Path.GetFullPath(outputPath));
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.CloseAndFlush();
}

public sealed class PageRecord
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; set; }
}