using JustKeyNew.Utility;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace JustKeyNew.Services
{
    public class PrintNodeService : IPrintNodeService
    {
        private readonly HttpClient _httpClient;
        private readonly PrintNodeSettings _settings;

        public PrintNodeService(HttpClient httpClient, IOptions<PrintNodeSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;

            var byteArray = Encoding.ASCII.GetBytes($"{_settings.ApiKey}:");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        public async Task<string> GetPrintersAsync()
        {
            var response = await _httpClient.GetAsync("https://api.printnode.com/printers");
            response.EnsureSuccessStatusCode(); // Başarılı yanıt kontrolü

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PrintFileAsync(string printerId, string filePath)
        {
            // Dosya uzantısını kontrol et
            var fileExtension = Path.GetExtension(filePath)?.ToLower();
            Console.WriteLine($"Dosya uzantısı: {fileExtension}");
            //if (fileExtension != ".txt")
            //{
            //    throw new NotSupportedException("Yalnızca .txt dosyaları desteklenmektedir.");
            //}

            // Dosyayı base64 formatına dönüştür
            var fileContent = await ConvertTextFileToBase64(filePath);

            var printJob = new
            {
                printerId = printerId,
                title = Path.GetFileName(filePath),
                contentType = "raw_base64", // contentType olarak raw_base64 kullanılıyor
                content = fileContent,
                source = "My Application"
            };

            var content = new StringContent(JsonConvert.SerializeObject(printJob), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.printnode.com/printjobs", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Print job failed with status code {response.StatusCode}: {error}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        private async Task<string> ConvertTextFileToBase64(string filePath)
        {
            var fileContent = await File.ReadAllTextAsync(filePath);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(fileContent));
        }
    }
}
