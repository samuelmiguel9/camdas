using BellucSketch.Domain.Enums;

namespace BellucSketch.Mobile.Services;

/// <summary>Implementação usada pelo Android (mestre): edições são sempre aplicadas direto, nunca
/// precisam de aprovação.</summary>
public sealed class PlataformaEdicaoDireta : IPlataformaEdicao
{
    public bool PrecisaAprovacao(TipoOperacaoEdicaoPendente tipoOperacao) => false;

    public Task<(string Responsavel, string Motivo)?> PedirJustificativaAsync() =>
        throw new NotSupportedException("Android aplica edições direto — nunca deveria pedir justificativa.");
}
