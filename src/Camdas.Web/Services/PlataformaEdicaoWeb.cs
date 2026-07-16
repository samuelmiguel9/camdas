using Camdas.Domain.Enums;
using Camdas.Mobile.Services;
using Microsoft.JSInterop;

namespace Camdas.Web.Services;

/// <summary>
/// A Web é auxiliar, não mestre — mas só edições que mexem no traço em si (desenhar por cima ou
/// apagar o desenho) exigem aprovação de um técnico no Android: visibilidade, opacidade, bloqueio e
/// prioridade entre camadas são livres, por não serem uma alteração crítica. Hoje a Web não tem
/// ferramenta de desenho, então "Excluir camada" (que apaga o traço junto) é a única operação da
/// lista que passa por aprovação — ver <see cref="PlantaViewModel.SolicitarOuExecutarAsync"/>. Pede
/// responsável e motivo via os mesmos prompts nativos do navegador já usados em <c>Planta.razor</c>.
/// </summary>
public sealed class PlataformaEdicaoWeb(IJSRuntime js) : IPlataformaEdicao
{
    public bool PrecisaAprovacao(TipoOperacaoEdicaoPendente tipoOperacao) =>
        tipoOperacao == TipoOperacaoEdicaoPendente.Excluir;

    public async Task<(string Responsavel, string Motivo)?> PedirJustificativaAsync()
    {
        var responsavel = await js.InvokeAsync<string?>("prompt", "Técnico responsável por esta edição:");
        if (string.IsNullOrWhiteSpace(responsavel))
            return null;

        var motivo = await js.InvokeAsync<string?>("prompt", "Motivo da edição:");
        if (string.IsNullOrWhiteSpace(motivo))
            return null;

        return (responsavel.Trim(), motivo.Trim());
    }
}
