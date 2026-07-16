using Camdas.Mobile.Services;

namespace Camdas.Web.Services;

/// <summary>
/// Camdas.Web é um site, não um APK instalável — o aviso de "nova versão disponível" (que aponta pro
/// GitHub Release do app Android) não faz sentido aqui. <see cref="ProjetosViewModel"/> exige um
/// <see cref="IVerificadorAtualizacao"/> por injeção de construtor; sem registrar algo, o DI do
/// Blazor falha ao criar a página "Projetos" inteira. Sempre retorna "sem atualização" (null).
/// </summary>
public sealed class VerificadorAtualizacaoNaoSuportado : IVerificadorAtualizacao
{
    public Task<AtualizacaoDisponivel?> VerificarAsync(CancellationToken ct = default) =>
        Task.FromResult<AtualizacaoDisponivel?>(null);
}
