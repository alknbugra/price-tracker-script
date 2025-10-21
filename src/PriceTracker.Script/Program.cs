using System.Globalization;
using System.Net.Http.Headers;
using CsvHelper;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Polly;
using Serilog;

Console.WriteLine("=== E-COMMERCE PRICE TRACKER ===");
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
Console.WriteLine($"Targets:Urls children count: {configuration.GetSection("Targets:Urls").GetChildren().Count()}");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    var urls = configuration.GetSection("Targets:Urls").Get<string[]>() ?? Array.Empty<string>();
    var enableCategoryMode = configuration.GetValue<bool>("CategorySettings:EnableCategoryMode", false);
    var maxProducts = configuration.GetValue<int>("CategorySettings:MaxProducts", 10);
    var productLinkSelector = configuration.GetValue<string>("CategorySettings:ProductLinkSelector", "//a[contains(@href, '/p-')]");
    
    Console.WriteLine($"URLs loaded: {urls.Length}");
    Console.WriteLine($"Category Mode: {enableCategoryMode}");
    Console.WriteLine($"Max Products: {maxProducts}");
    foreach (var url in urls) Console.WriteLine($"  - {url}");

    using var client = new HttpClient();
    client.Timeout = TimeSpan.FromSeconds(30);
    
    // Bot korumasını aşmak için gerçek browser User-Agent kullan
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr-TR,tr;q=0.9,en;q=0.8");
    // Accept-Encoding kaldırıldı - sıkıştırılmış içerik gelmesin
    client.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");
    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
    client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");

    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    var results = new List<PageRecord>();

    foreach (var url in urls)
    {
        if (enableCategoryMode)
        {
            // Kategori modu: Önce kategori sayfasından ürün linklerini çek
            Console.WriteLine($"\n🛍️ CATEGORY MODE: Fetching product links from {url}");
            var productUrls = await ExtractProductUrlsFromCategory(client, retryPolicy, url, productLinkSelector, maxProducts);
            
            Console.WriteLine($"Found {productUrls.Count} product URLs:");
            foreach (var productUrl in productUrls.Take(5))
            {
                Console.WriteLine($"  - {productUrl}");
            }
            if (productUrls.Count > 5) Console.WriteLine($"  ... and {productUrls.Count - 5} more");
            
            // Her ürünü işle (delay ile)
            for (int i = 0; i < productUrls.Count; i++)
            {
                await ProcessProduct(client, retryPolicy, productUrls[i], results);
                
                // Bot korumasını aşmak için delay
                if (i < productUrls.Count - 1)
                {
                    await Task.Delay(2000); // 2 saniye bekle
                }
            }
        }
        else
        {
            // Normal mod: Tek ürün işle
            await ProcessProduct(client, retryPolicy, url, results);
        }
    }

    var outputPath = configuration["Output:CsvPath"] ?? "output.csv";
    await using var writer = new StreamWriter(outputPath);
    await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
    await csv.WriteRecordsAsync(results);

    Log.Information("Wrote {Count} records to {Path}", results.Count, Path.GetFullPath(outputPath));
    Console.WriteLine($"\n📊 Total records: {results.Count}");
    Console.WriteLine($"📁 Output file: {Path.GetFullPath(outputPath)}");
    
    // HTML raporu oluştur
    var htmlPath = "product-report.html";
    await GenerateHtmlReport(results, htmlPath);
    Console.WriteLine($"🌐 HTML Report: {Path.GetFullPath(htmlPath)}");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.CloseAndFlush();
}

// HTML raporu oluşturma fonksiyonu
static async Task GenerateHtmlReport(List<PageRecord> results, string htmlPath)
{
    var html = $@"
<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>E-ticaret Ürün Raporu - {DateTime.Now:dd.MM.yyyy HH:mm}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 15px;
            box-shadow: 0 20px 40px rgba(0,0,0,0.1);
            overflow: hidden;
        }}
        
        .header {{
            background: linear-gradient(135deg, #1d1d47 0%, #3859ab  100%);
            color: white;
            padding: 30px;
            text-align: center;
        }}
        
        .header h1 {{
            font-size: 2.5em;
            margin-bottom: 10px;
        }}
        
        .header p {{
            font-size: 1.2em;
            opacity: 0.9;
        }}
        
        .stats {{
            display: flex;
            justify-content: space-around;
            padding: 20px;
            background: #f8f9fa;
            border-bottom: 1px solid #e9ecef;
        }}
        
        .stat-item {{
            text-align: center;
        }}
        
        .stat-number {{
            font-size: 2em;
            font-weight: bold;
            color: #667eea;
        }}
        
        .stat-label {{
            color: #6c757d;
            font-size: 0.9em;
        }}
        
        .products-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
            gap: 20px;
            padding: 30px;
        }}
        
        .product-card {{
            border: 1px solid #e9ecef;
            border-radius: 12px;
            overflow: hidden;
            transition: transform 0.3s ease, box-shadow 0.3s ease;
            background: white;
        }}
        
        .product-card:hover {{
            transform: translateY(-5px);
            box-shadow: 0 10px 25px rgba(0,0,0,0.15);
        }}
        
        .product-image {{
            width: 100%;
            height: 250px;
            object-fit: cover;
            background: #f8f9fa;
        }}
        
        .product-info {{
            padding: 20px;
        }}
        
        .product-name {{
            font-size: 1.1em;
            font-weight: 600;
            color: #2c3e50;
            margin-bottom: 10px;
            line-height: 1.4;
        }}
        
        .product-price {{
            font-size: 1.3em;
            font-weight: bold;
            color: #e74c3c;
            margin-bottom: 8px;
        }}
        
        .product-stock {{
            font-size: 0.9em;
            color: #27ae60;
            margin-bottom: 10px;
        }}
        
        .product-site {{
            display: inline-block;
            background: #667eea;
            color: white;
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 0.8em;
            font-weight: 500;
        }}
        
        .product-url {{
            margin-top: 10px;
        }}
        
        .product-url a {{
            color: #667eea;
            text-decoration: none;
            font-size: 0.9em;
            word-break: break-all;
        }}
        
        .product-url a:hover {{
            text-decoration: underline;
        }}
        
        .footer {{
            background: #2c3e50;
            color: white;
            text-align: center;
            padding: 20px;
            font-size: 0.9em;
        }}
        
        .no-products {{
            text-align: center;
            padding: 60px 20px;
            color: #6c757d;
        }}
        
        .no-products h3 {{
            font-size: 1.5em;
            margin-bottom: 10px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🤖 Script ile Veri Çekme Raporu</h1>
            <p>Otomatik Web Scraping Sistemi - {DateTime.Now:dd.MM.yyyy HH:mm}</p>
        </div>
        
        <div class=""stats"">
            <div class=""stat-item"">
                <div class=""stat-number"">{results.Count}</div>
                <div class=""stat-label"">Toplam Ürün</div>
            </div>
            <div class=""stat-item"">
                <div class=""stat-number"">{results.Count(r => !string.IsNullOrEmpty(r.Price))}</div>
                <div class=""stat-label"">Fiyat Bilgisi</div>
            </div>
            <div class=""stat-item"">
                <div class=""stat-number"">{results.Count(r => !string.IsNullOrEmpty(r.ImageUrl))}</div>
                <div class=""stat-label"">Resim Bilgisi</div>
            </div>
            <div class=""stat-item"">
                <div class=""stat-number"">{results.GroupBy(r => r.Site).Count()}</div>
                <div class=""stat-label"">Farklı Site</div>
            </div>
        </div>
        
        <div class=""products-grid"">
";

    if (results.Any())
    {
        foreach (var product in results)
        {
            var imageHtml = !string.IsNullOrEmpty(product.ImageUrl) 
                ? $@"<img src=""{product.ImageUrl}"" alt=""{product.ProductName}"" class=""product-image"" onerror=""this.src='data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjZjhmOWZhIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtZmFtaWx5PSJBcmlhbCIgZm9udC1zaXplPSIxNCIgZmlsbD0iIzZjNzU3ZCIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZHk9Ii4zZW0iPk5vIEltYWdlPC90ZXh0Pjwvc3ZnPg=='"">"
                : @"<div class=""product-image"" style=""background: #f8f9fa; display: flex; align-items: center; justify-content: center; color: #6c757d;"">No Image</div>";

            html += $@"
            <div class=""product-card"">
                {imageHtml}
                <div class=""product-info"">
                    <div class=""product-name"">{product.ProductName}</div>
                    <div class=""product-price"">{product.Price}</div>
                    <div class=""product-stock"">Stok Adedi: {product.StockStatus}</div>
                    <div class=""product-site"">{product.Site}</div>
                    <div class=""product-url"">
                        <a href=""{product.Url}"" target=""_blank"">Ürün Sayfasını Gör</a>
                    </div>
                </div>
            </div>";
        }
    }
    else
    {
        html += @"
            <div class=""no-products"">
                <h3>😔 Ürün Bulunamadı</h3>
                <p>Henüz hiç ürün çekilmedi. Lütfen ürün URL'lerini kontrol edin.</p>
            </div>";
    }

    html += @"
        </div>
        
        <div class=""footer"">
            <p>🤖 Price Tracker Script - .NET 9 ile geliştirilmiştir</p>
            <p>Rapor oluşturulma tarihi: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + @"</p>
        </div>
    </div>
</body>
</html>";

    await File.WriteAllTextAsync(htmlPath, html, System.Text.Encoding.UTF8);
}

// Kategori modu fonksiyonları
static async Task<List<string>> ExtractProductUrlsFromCategory(HttpClient client, IAsyncPolicy retryPolicy, string categoryUrl, string productLinkSelector, int maxProducts)
{
    var productUrls = new List<string>();
    
    try
    {
        Console.WriteLine($"Fetching category page: {categoryUrl}");
        var html = await retryPolicy.ExecuteAsync(async () => await client.GetStringAsync(categoryUrl));
        
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        // Ürün linklerini bul
        var productLinks = doc.DocumentNode.SelectNodes(productLinkSelector);
        if (productLinks != null)
        {
            foreach (var link in productLinks.Take(maxProducts))
            {
                var href = link.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    // Relative URL'leri absolute yap
                    if (href.StartsWith("/"))
                        href = "https://www.trendyol.com" + href;
                    else if (href.StartsWith("//"))
                        href = "https:" + href;
                    
                    if (href.Contains("/p-") && !productUrls.Contains(href))
                    {
                        productUrls.Add(href);
                    }
                }
            }
        }
        
        Console.WriteLine($"Extracted {productUrls.Count} product URLs from category");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting product URLs: {ex.Message}");
    }
    
    return productUrls;
}

static async Task ProcessProduct(HttpClient client, IAsyncPolicy retryPolicy, string url, List<PageRecord> results)
{
    try
    {
        Log.Information("Fetching {Url}", url);
        var html = await retryPolicy.ExecuteAsync(async () => await client.GetStringAsync(url));
        
        // HTML'i dosyaya kaydet (debug için)
        await File.WriteAllTextAsync("debug.html", html);
        Console.WriteLine($"📄 HTML saved to debug.html ({html.Length} characters)");
        
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        // Temel bilgileri çek
        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? string.Empty;
        
        // E-ticaret siteleri için özel parsing
        var productName = ExtractProductName(doc, url);
        var price = ExtractPrice(doc, url);
        var stockStatus = ExtractStockStatus(doc, url);
        var imageUrl = ExtractImageUrl(doc, url);
        var site = ExtractSiteName(url);

        // Debug: HTML içeriğini kontrol et
        Console.WriteLine($"\n🔍 DEBUG INFO for {site}:");
        Console.WriteLine($"Title: {title}");
        Console.WriteLine($"Product Name: {productName}");
        Console.WriteLine($"Price: {price}");
        Console.WriteLine($"Stock: {stockStatus}");
        Console.WriteLine($"Image: {imageUrl}");
        
        // HTML'den örnek elementler bul
        var h1Elements = doc.DocumentNode.SelectNodes("//h1");
        if (h1Elements != null)
        {
            Console.WriteLine($"Found {h1Elements.Count} H1 elements:");
            foreach (var h1 in h1Elements.Take(3))
            {
                Console.WriteLine($"  - H1: {h1.InnerText?.Trim()}");
            }
        }
        
        var priceElements = doc.DocumentNode.SelectNodes("//span[contains(text(), '₺')]");
        if (priceElements != null)
        {
            Console.WriteLine($"Found {priceElements.Count} price elements:");
            foreach (var priceEl in priceElements.Take(3))
            {
                Console.WriteLine($"  - Price: {priceEl.InnerText?.Trim()}");
            }
        }
        
        var imageElements = doc.DocumentNode.SelectNodes("//img[contains(@class, 'product') or contains(@class, 'prd')]");
        if (imageElements != null)
        {
            Console.WriteLine($"Found {imageElements.Count} product image elements:");
            foreach (var imgEl in imageElements.Take(3))
            {
                var src = imgEl.GetAttributeValue("src", "");
                Console.WriteLine($"  - Image: {src}");
            }
        }

        results.Add(new PageRecord 
        { 
            Url = url, 
            Title = title,
            ProductName = productName,
            Price = price,
            StockStatus = stockStatus,
            ImageUrl = imageUrl,
            Site = site,
            RetrievedAt = DateTimeOffset.UtcNow 
        });

        Console.WriteLine($"✅ {site}: {productName} - {price}\n");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to fetch {Url}", url);
        Console.WriteLine($"❌ Failed to fetch: {url}");
    }
}

// E-ticaret parsing fonksiyonları
static string ExtractProductName(HtmlDocument doc, string url)
{
    try
    {
        if (url.Contains("trendyol.com"))
        {
            // Trendyol için ürün adı seçicileri (güncellenmiş)
            var productName = doc.DocumentNode.SelectSingleNode("//h1[@class='pr-new-br']")?.InnerText?.Trim() ??
                             doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'product-name')]")?.InnerText?.Trim() ??
                             doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'prd-name')]")?.InnerText?.Trim() ??
                             doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'pr-title')]")?.InnerText?.Trim() ??
                             doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim() ??
                             doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
            return productName ?? string.Empty;
        }
        else if (url.Contains("hepsiburada.com"))
        {
            // Hepsiburada için ürün adı seçicileri
            var productName = doc.DocumentNode.SelectSingleNode("//h1[@id='product-name']")?.InnerText?.Trim() ??
                             doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'product-name')]")?.InnerText?.Trim() ??
                             doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();
            return productName ?? string.Empty;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting product name: {ex.Message}");
    }
    return string.Empty;
}

static string ExtractPrice(HtmlDocument doc, string url)
{
    try
    {
        if (url.Contains("trendyol.com"))
        {
            // Trendyol için fiyat seçicileri (güncellenmiş)
            var price = doc.DocumentNode.SelectSingleNode("//span[@class='prc-org']")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'prc-slg')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'prc-dsc')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'price')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'prc')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'current-price')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'price')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(text(), '₺')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//div[contains(text(), '₺')]")?.InnerText?.Trim();
            return price ?? string.Empty;
        }
        else if (url.Contains("hepsiburada.com"))
        {
            // Hepsiburada için fiyat seçicileri
            var price = doc.DocumentNode.SelectSingleNode("//span[@id='offering-price']")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'price')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(text(), '₺')]")?.InnerText?.Trim();
            return price ?? string.Empty;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting price: {ex.Message}");
    }
    return string.Empty;
}

static string ExtractStockStatus(HtmlDocument doc, string url)
{
    try
    {
        if (url.Contains("trendyol.com"))
        {
            // Trendyol için stok durumu seçicileri
            var stock = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'stock')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(text(), 'Stok')]")?.InnerText?.Trim();
            return stock ?? "Bilinmiyor";
        }
        else if (url.Contains("hepsiburada.com"))
        {
            // Hepsiburada için stok durumu seçicileri
            var stock = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'stock')]")?.InnerText?.Trim() ??
                       doc.DocumentNode.SelectSingleNode("//span[contains(text(), 'Stok')]")?.InnerText?.Trim();
            return stock ?? "Bilinmiyor";
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting stock status: {ex.Message}");
    }
    return "Bilinmiyor";
}

static string ExtractImageUrl(HtmlDocument doc, string url)
{
    try
    {
        if (url.Contains("trendyol.com"))
        {
            // Trendyol için resim URL seçicileri
            var imageUrl = doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'product-image')]")?.GetAttributeValue("src", "") ??
                          doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'prd-img')]")?.GetAttributeValue("src", "") ??
                          doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'product')]")?.GetAttributeValue("src", "") ??
                          doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'main')]")?.GetAttributeValue("src", "") ??
                          doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'hero')]")?.GetAttributeValue("src", "");
            
            // Relative URL'leri absolute yap
            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("//"))
                imageUrl = "https:" + imageUrl;
            else if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("/"))
                imageUrl = "https://www.trendyol.com" + imageUrl;
                
            return imageUrl ?? string.Empty;
        }
        else if (url.Contains("hepsiburada.com"))
        {
            // Hepsiburada için resim URL seçicileri
            var imageUrl = doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'product-image')]")?.GetAttributeValue("src", "") ??
                          doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'product')]")?.GetAttributeValue("src", "") ??
                          doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'main')]")?.GetAttributeValue("src", "");
            
            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("//"))
                imageUrl = "https:" + imageUrl;
            else if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("/"))
                imageUrl = "https://www.hepsiburada.com" + imageUrl;
                
            return imageUrl ?? string.Empty;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting image URL: {ex.Message}");
    }
    return string.Empty;
}

static string ExtractSiteName(string url)
{
    if (url.Contains("trendyol.com")) return "Trendyol";
    if (url.Contains("hepsiburada.com")) return "Hepsiburada";
    if (url.Contains("n11.com")) return "N11";
    if (url.Contains("gittigidiyor.com")) return "GittiGidiyor";
    return "Bilinmiyor";
}

public sealed class PageRecord
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string StockStatus { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; set; }
}