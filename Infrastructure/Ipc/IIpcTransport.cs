using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Ipc
{
    public interface IIpcTransport
    {
        Task<IpcResponse?> SendAsync(IpcMessage message, CancellationToken cancellationToken = default);
    }
}
