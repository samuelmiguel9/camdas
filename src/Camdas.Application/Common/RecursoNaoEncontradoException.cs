namespace Camdas.Application.Common;

/// <summary>
/// Sinaliza que um recurso solicitado não existe. A Api mapeia isso para HTTP 404.
/// </summary>
public sealed class RecursoNaoEncontradoException : Exception
{
    public RecursoNaoEncontradoException(string mensagem) : base(mensagem)
    {
    }
}
