using BellucSketch.Application.Abstractions;

namespace BellucSketch.Infrastructure;

public sealed class RelogioSistema : IClock
{
    public DateTime AgoraUtc => DateTime.UtcNow;
}
