namespace Camdas.Domain.Common;

/// <summary>
/// Sinaliza violação de uma regra de negócio do domínio (não um erro técnico/infra).
/// A camada de apresentação (API) deve mapear isso para um status HTTP apropriado (400/409).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
