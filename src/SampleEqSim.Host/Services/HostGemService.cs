using Microsoft.Extensions.Hosting;
using Secs4Net;

namespace SampleEqSim.Host.Services;

/// <summary>
/// ホスト側の SECS/GEM メッセージループサービス
/// GetPrimaryMessageAsync を使って装置からの Primary メッセージを受信し、
/// ViewModel へ転送する
/// </summary>
public sealed class HostGemService : IHostedService
{
    private readonly ISecsGem _secsGem;
    private readonly ISecsConnection _connection;
    private CancellationTokenSource? _cts;

    // ─── 外部公開イベント ─────────────────────────────────────────
    /// <summary>HSMS 接続状態変化</summary>
    public event EventHandler<ConnectionState>? ConnectionChanged;

    /// <summary>装置からの Primary メッセージ受信 (S5F1, S6F11, S10F1 等)</summary>
    public event Func<PrimaryMessageWrapper, Task>? PrimaryMessageReceived;

    public HostGemService(ISecsGem secsGem, ISecsConnection connection)
    {
        _secsGem = secsGem;
        _connection = connection;

        // 接続状態変化を購読 (ISecsConnection は ISecsGem とは別オブジェクト)
        _connection.ConnectionChanged += OnConnectionChanged;
    }

    private void OnConnectionChanged(object? sender, ConnectionState state)
        => ConnectionChanged?.Invoke(sender, state);

    // ─── IHostedService ──────────────────────────────────────────
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RunMessageLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task RunMessageLoopAsync(CancellationToken ct)
    {
        try
        {
            // HSMS 接続を開始 (Active: 装置へ接続を試行)。
            // これを呼ばないと接続状態機械が動かず、装置へ接続しに行かない。
            _connection.Start(ct);

            await foreach (var e in _secsGem.GetPrimaryMessageAsync(ct))
            {
                if (PrimaryMessageReceived != null)
                    await PrimaryMessageReceived(e);
            }
        }
        catch (OperationCanceledException) { /* 正常終了 */ }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[HostGemService] メッセージループエラー: {ex}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }
}
