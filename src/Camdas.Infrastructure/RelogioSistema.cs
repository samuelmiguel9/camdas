using Camdas.Application.Abstractions;

namespace Camdas.Infrastructure;

public sealed class RelogioSistema : IClock
{
    public DateTime AgoraUtc => DateTime.UtcNow;
}
