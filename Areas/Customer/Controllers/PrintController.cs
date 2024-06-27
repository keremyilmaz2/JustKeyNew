using JustKeyNew.Services;
using Microsoft.AspNetCore.Mvc;

[Area("Customer")]
public class PrintController : Controller
{
    private readonly IPrintNodeService _printNodeService;

    public PrintController(IPrintNodeService printNodeService)
    {
        _printNodeService = printNodeService;
    }

    public async Task<IActionResult> Index()
    {
        var printers = await _printNodeService.GetPrintersAsync();
        ViewBag.Printers = printers;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Print(string printerId, IFormFile file)
    {
        if (file != null && file.Length > 0)
        {
            var filePath = Path.GetTempFileName();
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var result = await _printNodeService.PrintFileAsync(printerId, filePath);
            ViewBag.Result = result;
            return View("Result");
        }
        return BadRequest("Invalid file");
    }
}
