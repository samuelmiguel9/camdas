using Camdas.Mobile.Services;

namespace Camdas.Web.Services;

/// <summary>
/// Camdas.Web é só visualização — não tem o botão "Salvar tudo (PNG)" (só existe no app Android),
/// mas <see cref="PlantaViewModel"/> exige um <see cref="ISalvadorGaleria"/> por injeção de
/// construtor. Sem registrar algo aqui, o DI do Blazor falha ao criar a página inteira com uma
/// exceção não tratada ("Unable to resolve service for type ISalvadorGaleria") — foi a causa real da
/// tela de erro genérica do Blazor no navegador.
/// </summary>
public sealed class SalvadorGaleriaNaoSuportado : ISalvadorGaleria
{
    public Task SalvarPngAsync(byte[] pngBytes, string nomeArquivo, CancellationToken ct = default) =>
        throw new NotSupportedException("Salvar na galeria só está disponível no app Android.");
}
