namespace Camdas.Mobile.Services;

public sealed record AtualizacaoDisponivel(string Versao, string UrlDownload);

/// <summary>Consulta o repositório público no GitHub por uma versão mais nova que a instalada.</summary>
public interface IVerificadorAtualizacao
{
    /// <summary>Retorna null se não há atualização (ou se a checagem falhar — nunca lança, checagem
    /// de atualização não pode travar o app nem exibir erro pro usuário).</summary>
    Task<AtualizacaoDisponivel?> VerificarAsync(CancellationToken ct = default);
}
