using Secs4Net;
using Microsoft.Extensions.Logging;

namespace SampleEqSim.Host;

/// <summary>
/// secs4net が要求する ISecsGemLogger の実装
/// </summary>
internal sealed class DeviceLogger : ISecsGemLogger
{
    private readonly ILogger<DeviceLogger> _logger;

    public DeviceLogger(ILogger<DeviceLogger> logger)
    {
        _logger = logger;
    }

    public void MessageIn(SecsMessage msg, int id) =>
        _logger.LogDebug("<-- [0x{Id:X8}] {Msg}", id, msg);

    public void MessageOut(SecsMessage msg, int id) =>
        _logger.LogDebug("--> [0x{Id:X8}] {Msg}", id, msg);

    public void Debug(string msg) =>
        _logger.LogDebug("{Msg}", msg);

    public void Info(string msg) =>
        _logger.LogInformation("{Msg}", msg);

    public void Warning(string msg) =>
        _logger.LogWarning("{Msg}", msg);

    public void Error(string msg) =>
        _logger.LogError("{Msg}", msg);

    public void Error(string msg, Exception ex) =>
        _logger.LogError(ex, "{Msg}", msg);

    public void Error(string msg, SecsMessage? message, Exception? ex) =>
        _logger.LogError(ex, "{Msg} | SecsMessage: {SecsMsg}", msg, message);
}
