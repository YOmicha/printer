using System.Net.Sockets;
using System.IO.Ports;
using System.Text;

namespace printer.Services;

public interface IPrintService
{
    Task<bool> PrintZplAsync(string zplContent, string printerIp, int port = 9100);
    Task<bool> PrintZplToPortAsync(string zplContent, string portName);
    List<string> GetAvailablePorts();
}

public class PrintService : IPrintService
{
    private readonly ILogger<PrintService> _logger;

    public PrintService(ILogger<PrintService> logger)
    {
        _logger = logger;
    }

    public List<string> GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available ports");
            return new List<string>();
        }
    }

    public async Task<bool> PrintZplAsync(string zplContent, string printerIp, int port = 9100)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(printerIp, port);

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream);

            await writer.WriteAsync(zplContent);
            await writer.FlushAsync();

            _logger.LogInformation("ZPL content sent successfully to printer at {PrinterIp}:{Port}", printerIp, port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ZPL content to printer at {PrinterIp}:{Port}", printerIp, port);
            return false;
        }
    }

    public async Task<bool> PrintZplToPortAsync(string zplContent, string portName)
    {
        try
        {
            using var serialPort = new SerialPort(portName)
            {
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None
            };

            serialPort.Open();
            
            if (!serialPort.IsOpen)
            {
                throw new Exception($"Could not open port {portName}");
            }

            // Convert the ZPL content to bytes
            byte[] data = Encoding.ASCII.GetBytes(zplContent);

            // Write the data to the serial port
            await Task.Run(() => {
                serialPort.Write(data, 0, data.Length);
                serialPort.BaseStream.Flush();
            });
            
            _logger.LogInformation("ZPL content sent successfully to printer on port {PortName}", portName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ZPL content to printer on port {PortName}", portName);
            return false;
        }
    }
} 