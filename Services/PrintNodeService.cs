using JustKeyNew.Utility;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

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
            var response = await _httpClient.GetAsync("printers");
            response.EnsureSuccessStatusCode(); // Başarılı yanıt kontrolü

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PrintFileAsync(string printerId, string filePath)
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileContent = Convert.ToBase64String(fileBytes);

            var printJob = new
            {
                printerId = printerId,
                title = Path.GetFileName(filePath),
                contentType = "raw_base64",
                content = fileContent,
                source = "My Application"
            };

            var content = new StringContent(JsonConvert.SerializeObject(printJob), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("printjobs", content);
            response.EnsureSuccessStatusCode(); // Başarılı yanıt kontrolü

            return await response.Content.ReadAsStringAsync();
        }
    }
}
