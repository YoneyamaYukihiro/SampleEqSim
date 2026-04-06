using Secs4Net;

namespace SampleEqSim.Core.Secs;

public sealed class NoopSecsGem : ISecsGem
{
    public IAsyncEnumerable<PrimaryMessageWrapper> GetPrimaryMessageAsync(CancellationToken cancellation = default)
    {
        return Empty();

        static async IAsyncEnumerable<PrimaryMessageWrapper> Empty()
        {
            yield break;
        }
    }

    public Task<SecsMessage> SendAsync(SecsMessage message, CancellationToken cancellation = default)
    {
        return Task.FromResult<SecsMessage>(null!);
    }
}
