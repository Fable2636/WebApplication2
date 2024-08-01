using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Xml.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Параметры
string sitemapUrl = builder.Configuration["SITEMAP_URL"] ?? "https://cska-tickets.com/sitemap.xml"; // URL карты сайта
int maxConcurrentTabs = int.TryParse(builder.Configuration["MAX_CONCURRENT_TABS"], out var tabs) ? tabs : 3; // Максимальное количество одновременно открытых вкладок
int pageWaitTime = int.TryParse(builder.Configuration["PAGE_WAIT_TIME"], out var waitTime) ? waitTime : 5000; // Интервал ожидания на странице в миллисекундах
int repeatInterval = int.TryParse(builder.Configuration["REPEAT_INTERVAL"], out var interval) ? interval : 10; // Интервал повторного обхода в минутах
var options = new ChromeOptions();
options.AddArgument("--no-sandbox"); // Отключение песочницы
options.AddArgument("--headless"); // Запуск в фоновом режиме
options.AddArgument("--disable-dev-shm-usage"); // Использование временной файловой системы
options.AddArgument("--disable-gpu"); // Отключение GPU
options.AddArgument("--disable-software-rasterizer"); // Отключение программного рендеринга

app.MapGet("/", async () =>
{
    using (IWebDriver driver = new ChromeDriver())
    {
        while (true)
        {
            using (HttpClient client = new HttpClient())
            {
                // Получаем XML-карту сайта
                string xmlContent = await client.GetStringAsync(sitemapUrl);

                // Загружаем XML данные
                XDocument xmlDoc = XDocument.Parse(xmlContent);

                // Извлекаем ссылки
                var links = xmlDoc.Descendants(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9"))
                                  .Select(loc => loc.Value)
                                  .ToList();

                // Используем SemaphoreSlim для ограничения количества одновременно открытых вкладок
                using (SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrentTabs))
                {
                    List<Task> tasks = new List<Task>();

                    foreach (var link in links)
                    {
                        await semaphore.WaitAsync(); // Ожидаем, пока не освободится место

                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                Console.WriteLine($"Открываем: {link}");
                                driver.Navigate().GoToUrl(link);
                                await Task.Delay(pageWaitTime); // Ожидание на странице
                            }
                            finally
                            {
                                semaphore.Release(); // Освобождаем место
                            }
                        }));
                    }

                    await Task.WhenAll(tasks); // Ждем завершения всех задач
                }
            }

            // Ожидание перед повторным обходом
            Thread.Sleep(TimeSpan.FromMinutes(repeatInterval));
        }
    }
});

app.Run();
