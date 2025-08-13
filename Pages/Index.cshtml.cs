using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using printer.Services;

namespace printer.Pages;

public class PrintRequest
{
    public string PrinterIp { get; set; } = "";
    public int PrinterPort { get; set; }
    public string SerialPort { get; set; } = "";
    public string ZplContent { get; set; } = "";
    public bool UseSerialPort { get; set; }
}

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IPrintService _printService;

    [BindProperty]
    public IFormFile? ZplFile { get; set; }

    public string? ZplContent { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public string PrinterIp { get; set; } = "192.168.1.100";

    [BindProperty]
    public int PrinterPort { get; set; } = 9100;

    public List<string> AvailablePorts { get; private set; } = new();

    public IndexModel(ILogger<IndexModel> logger, IPrintService printService)
    {
        _logger = logger;
        _printService = printService;
    }

    public void OnGet()
    {
        AvailablePorts = _printService.GetAvailablePorts();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AvailablePorts = _printService.GetAvailablePorts();

        if (ZplFile == null || ZplFile.Length == 0)
        {
            ErrorMessage = "Please select a file";
            return Page();
        }

        if (!ZplFile.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Please upload a .txt file";
            return Page();
        }

        try
        {
            using var reader = new StreamReader(ZplFile.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            
            content = content.Trim()
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n\n", "\n");

            if (!content.StartsWith("^XA", StringComparison.OrdinalIgnoreCase) ||
                !content.EndsWith("^XZ", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Invalid ZPL format. File must start with ^XA and end with ^XZ";
                return Page();
            }

            ZplContent = content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZPL file");
            ErrorMessage = "Error processing the file. Please ensure it contains valid ZPL code.";
            return Page();
        }
        
        return Page();
    }

    public async Task<IActionResult> OnPostPrintAsync([FromBody] PrintRequest request)
    {
        if (string.IsNullOrEmpty(request.ZplContent))
        {
            return new JsonResult(new { success = false, message = "No ZPL content to print" });
        }

        try
        {
            bool success;
            if (request.UseSerialPort)
            {
                if (string.IsNullOrEmpty(request.SerialPort))
                {
                    return new JsonResult(new { success = false, message = "No serial port selected" });
                }
                success = await _printService.PrintZplToPortAsync(request.ZplContent, request.SerialPort);
            }
            else
            {
                success = await _printService.PrintZplAsync(request.ZplContent, request.PrinterIp, request.PrinterPort);
            }

            if (success)
            {
                return new JsonResult(new { success = true, message = "Print job sent successfully" });
            }
            else
            {
                return new JsonResult(new { success = false, message = "Failed to send print job" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing ZPL content");
            return new JsonResult(new { success = false, message = "Error sending print job: " + ex.Message });
        }
    }
}
