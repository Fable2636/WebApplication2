using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Xml.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ���������
string sitemapUrl = builder.Configuration["SITEMAP_URL"] ?? "https://cska-tickets.com/sitemap.xml"; // URL ����� �����
int maxConcurrentTabs = int.TryParse(builder.Configuration["MAX_CONCURRENT_TABS"], out var tabs) ? tabs : 3; // ������������ ���������� ������������ �������� �������
int pageWaitTime = int.TryParse(builder.Configuration["PAGE_WAIT_TIME"], out var waitTime) ? waitTime : 5000; // �������� �������� �� �������� � �������������
int repeatInterval = int.TryParse(builder.Configuration["REPEAT_INTERVAL"], out var interval) ? interval : 10; // �������� ���������� ������ � �������
var options = new ChromeOptions();
options.AddArgument("--no-sandbox"); // ���������� ���������
options.AddArgument("--headless"); // ������ � ������� ������
options.AddArgument("--disable-dev-shm-usage"); // ������������� ��������� �������� �������
options.AddArgument("--disable-gpu"); // ���������� GPU
options.AddArgument("--disable-software-rasterizer"); // ���������� ������������ ����������

app.MapGet("/", async () =>
{
    using (IWebDriver driver = new ChromeDriver())
    {
        while (true)
        {
            using (HttpClient client = new HttpClient())
            {
                // �������� XML-����� �����
                string xmlContent = await client.GetStringAsync(sitemapUrl);

                // ��������� XML ������
                XDocument xmlDoc = XDocument.Parse(xmlContent);

                // ��������� ������
                var links = xmlDoc.Descendants(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9"))
                                  .Select(loc => loc.Value)
                                  .ToList();

                // ���������� SemaphoreSlim ��� ����������� ���������� ������������ �������� �������
                using (SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrentTabs))
                {
                    List<Task> tasks = new List<Task>();

                    foreach (var link in links)
                    {
                        await semaphore.WaitAsync(); // �������, ���� �� ����������� �����

                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                Console.WriteLine($"���������: {link}");
                                driver.Navigate().GoToUrl(link);
                                await Task.Delay(pageWaitTime); // �������� �� ��������
                            }
                            finally
                            {
                                semaphore.Release(); // ����������� �����
                            }
                        }));
                    }

                    await Task.WhenAll(tasks); // ���� ���������� ���� �����
                }
            }

            // �������� ����� ��������� �������
            Thread.Sleep(TimeSpan.FromMinutes(repeatInterval));
        }
    }
});

app.Run();
