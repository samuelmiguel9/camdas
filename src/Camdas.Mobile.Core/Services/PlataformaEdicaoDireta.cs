namespace Camdas.Mobile.Services;

/// <summary>Implementação usada pelo Android (mestre): edições são sempre aplicadas direto, nunca
/// precisam de aprovação.</summary>
public sealed class PlataformaEdicaoDireta : IPlataformaEdicao
{
    public bool ExigeAprovacao => false;

    public Task<(string Responsavel, string Motivo)?> PedirJustificativaAsync() =>
        throw new NotSupportedException("Android aplica edições direto — nunca deveria pedir justificativa.");
}
