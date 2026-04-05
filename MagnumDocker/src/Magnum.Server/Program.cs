using System.CommandLine;
using Magnum.Serial;
using Magnum.Server;

var builder = WebApplication.CreateBuilder(args);

var magnumConfig = builder.Configuration.GetSection("Magnum");

int port = magnumConfig.GetValue("Port", 17223);
string listen = magnumConfig.GetValue("Listen", "") ?? "";
var devices = magnumConfig.GetSection("Devices").Get<string[]>() ?? [];
int packets = magnumConfig.GetValue("Packets", 50);
double timeout = magnumConfig.GetValue("Timeout", 0.005);
bool trace = magnumConfig.GetValue("Trace", false);
bool cleanPackets = magnumConfig.GetValue("CleanPackets", true);
bool verbose = magnumConfig.GetValue("Verbose", false);

var portOption = new Option<int?>("--port", "Server listening port");
var deviceOption = new Option<string[]>("--device", "Serial device paths") { AllowMultipleArgumentsPerToken = true };
var listenOption = new Option<string?>("--listen", "IP address to listen on");
var packetsOption = new Option<int?>("--packets", "Packet buffer size");
var timeoutOption = new Option<double?>("--timeout", "Serial read timeout in seconds");
var traceOption = new Option<bool>("--trace", "Include raw packet info");
var noCleanupOption = new Option<bool>("--nocleanup", "Suppress packet cleanup");
var verboseOption = new Option<bool>("--verbose", "Display options at runtime");

var rootCommand = new RootCommand("Magnum Network Reader");
rootCommand.AddOption(portOption);
rootCommand.AddOption(deviceOption);
rootCommand.AddOption(listenOption);
rootCommand.AddOption(packetsOption);
rootCommand.AddOption(timeoutOption);
rootCommand.AddOption(traceOption);
rootCommand.AddOption(noCleanupOption);
rootCommand.AddOption(verboseOption);

rootCommand.SetHandler((portVal, deviceVal, listenVal, packetsVal, timeoutVal, traceVal, noCleanupVal, verboseVal) =>
{
    if (portVal.HasValue) port = portVal.Value;
    if (deviceVal.Length > 0) devices = deviceVal;
    if (listenVal != null) listen = listenVal.ToUpperInvariant() == "ALL" ? "" : listenVal;
    if (packetsVal.HasValue) packets = packetsVal.Value;
    if (timeoutVal.HasValue) timeout = timeoutVal.Value;
    if (traceVal) trace = true;
    if (noCleanupVal) cleanPackets = false;
    if (verboseVal) verbose = true;
}, portOption, deviceOption, listenOption, packetsOption, timeoutOption, traceOption, noCleanupOption, verboseOption);

rootCommand.Invoke(args);

if (listen.Equals("ALL", StringComparison.OrdinalIgnoreCase))
    listen = "";

if (devices.Length == 0)
    devices = ["/dev/ttyUSB0"];

if (verbose)
{
    Console.WriteLine($"Options: port={port}, listen='{listen}', devices=[{string.Join(", ", devices)}], " +
        $"packets={packets}, timeout={timeout}, trace={trace}, cleanpackets={cleanPackets}");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .WithMethods("GET", "OPTIONS")
              .WithHeaders("Content-Type");
    });
});

builder.Services.AddSingleton<MagnumService>();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

var app = builder.Build();
app.UseCors();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var magnumService = app.Services.GetRequiredService<MagnumService>();

foreach (var device in devices)
{
    try
    {
        var options = new MagnumReaderOptions
        {
            Device = device,
            Packets = packets,
            Timeout = timeout,
            Trace = trace,
            CleanPackets = cleanPackets,
        };

        var reader = new MagnumReader(options);
        reader.GetDevices();
        magnumService.AddReader(reader);
        logger.LogInformation("Initialized reader for {Device}", device);
    }
    catch (Exception ex)
    {
        logger.LogError("{Device} {Error}", device, ex.Message);
    }
}

if (magnumService.ReaderCount == 0)
{
    logger.LogCritical("Error: There are no usable devices connected.");
    return;
}

app.MapGet("/", () => Results.File("wwwroot/index.html", "text/html; charset=utf-8"));
app.MapGet("/index.html", () => Results.File("wwwroot/index.html", "text/html; charset=utf-8"));
app.MapGet("/api", (MagnumService svc) => svc.GetAllDeviceData());

Console.WriteLine($"Starting Magnum Reader on {(string.IsNullOrEmpty(listen) ? "0.0.0.0" : listen)}:{port}");
app.Run();
