namespace Camdas.Application.Abstractions;

/// <summary>
/// Abstrai a obtenção do instante atual, permitindo testar casos de uso com data controlada.
/// </summary>
public interface IClock
{
    DateTime AgoraUtc { get; }
}
