using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Cats_practice
{
    [Route("api/[controller]")]
    [ApiController]
    public class HttpCatController : ControllerBase
    {
        // кэш для хранения изображений 
        private static readonly Dictionary<int, byte[]> imageCache = new Dictionary<int, byte[]>();
        
        // семафор для синхронизации доступа к кжшу
        private static readonly SemaphoreSlim cacheLock = new SemaphoreSlim(1, 1);

        private readonly IHttpClientFactory _httpClientFactory;

        public HttpCatController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetHttpCat([FromQuery] string url)
        {
            int statusCode;
            
            if (string.IsNullOrWhiteSpace(url))
            {
                statusCode = 400;
            }
            else
            {
                try
                {
                    // получение статус кода
                    statusCode = await GetStatusCodeFromUrlAsync(url);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, ex.Message);
                }
            }

            // Получаем и кэшируем изображение с котиком
            return await GetHttpCatImage(statusCode);
        }

        // получение статус кода
        private async Task<int> GetStatusCodeFromUrlAsync(string url)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url);
            return (int)response.StatusCode;
        }

        // метод для получения изображения с
        private async Task<byte[]> GetImageFromHttpCatAsync(int statusCode)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync($"https://http.cat/{statusCode}");
            return await response.Content.ReadAsByteArrayAsync();
        }

        // кэширование изображения
        private async Task CacheImageAsync(int statusCode, byte[] image)
        {
            await cacheLock.WaitAsync();
            try
            {
                // добавление изображения в кэш
                imageCache[statusCode] = image;

                // удаление через 5 минут изображения в кэше
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    await cacheLock.WaitAsync();
                    try
                    {
                        imageCache.Remove(statusCode);
                    }
                    finally
                    {
                        cacheLock.Release();
                    }
                });
            }
            finally
            {
                cacheLock.Release();
            }
        }

        // метод для проверки изображения, есть ли в кэше, или получение
        private async Task<IActionResult> GetHttpCatImage(int statusCode)
        {
            // проверка изображения в кэше
            if (imageCache.TryGetValue(statusCode, out var cachedImage))
            {
                return File(cachedImage, "image/jpeg");
            }

            // получаем изображение котика
            byte[] image = await GetImageFromHttpCatAsync(statusCode);

            // кэширование изображения
            await CacheImageAsync(statusCode, image);

            return File(image, "image/jpeg");
        }
    }
}
