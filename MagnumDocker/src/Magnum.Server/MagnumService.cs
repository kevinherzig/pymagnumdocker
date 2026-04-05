using Magnum.Serial;

namespace Magnum.Server;

public class MagnumService : IDisposable
{
    private readonly Dictionary<string, MagnumReader> _readers = new();
    private readonly ILogger<MagnumService> _logger;

    public MagnumService(ILogger<MagnumService> logger)
    {
        _logger = logger;
    }

    public void AddReader(MagnumReader reader)
    {
        _readers[reader.CommDevice] = reader;
    }

    public int ReaderCount => _readers.Count;

    public IResult GetAllDeviceData()
    {
        var response = new List<object>();
        int? errorCode = null;
        string? errorMessage = null;

        var timestamp = DateTimeOffset.UtcNow
            .ToLocalTime()
            .ToString("yyyy-MM-ddTHH:mm:sszzz");

        foreach (var (commDevice, reader) in _readers)
        {
            try
            {
                var devices = reader.GetDevices();
                if (devices.Count > 0)
                {
                    response.Add(new
                    {
                        comm_device = commDevice,
                        data = new
                        {
                            datetime = timestamp,
                            devices = devices.Select(d => new
                            {
                                device = d.Device,
                                data = d.Data,
                            }),
                        },
                    });
                }
                else
                {
                    errorMessage = "No data available";
                    errorCode = 204;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Exception detected attempting to read network data - {commDevice} {ex.Message}";
                errorCode = 500;
                _logger.LogError(ex, "Error reading from {Device}", commDevice);
            }
        }

        if (response.Count > 0)
            return Results.Json(response);

        return errorCode == 204
            ? Results.NoContent()
            : Results.Problem(errorMessage, statusCode: errorCode ?? 500);
    }

    public void Dispose()
    {
        foreach (var reader in _readers.Values)
            reader.Dispose();
    }
}
