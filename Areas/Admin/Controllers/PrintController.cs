using JustKeyNew.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using JustKeyNew.Utility;

[Area("Admin")]
public class PrintController : Controller
{
    private readonly IPrintNodeService _printNodeService;
    private readonly IConfiguration _configuration;
    private readonly IOptions<PrinterSettings> _printerSettings;
    private readonly IWebHostEnvironment _environment;

    public PrintController(IPrintNodeService printNodeService, IConfiguration configuration, IOptions<PrinterSettings> printerSettings, IWebHostEnvironment environment)
    {
        _printNodeService = printNodeService;
        _configuration = configuration;
        _printerSettings = printerSettings;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var printers = await _printNodeService.GetPrintersAsync();
        ViewBag.Printers = printers;
        ViewBag.PrinterId = _printerSettings.Value.PrinterId;
        return View();
    }

    [HttpPost]
    public IActionResult Print(string printerId)
    {
        if (!string.IsNullOrEmpty(printerId))
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
            var jsonConfig = System.IO.File.ReadAllText(filePath);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonConfig);

            jsonObj["PrinterSettings"]["PrinterId"] = printerId;

            string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(filePath, output);

            ViewBag.PrinterId = printerId;
            return View("Result");
        }
        return BadRequest("Geçersiz yazıcı ID'si");
    }
}
