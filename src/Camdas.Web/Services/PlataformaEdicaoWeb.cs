using Camdas.Mobile.Services;
using Microsoft.JSInterop;

namespace Camdas.Web.Services;

/// <summary>
/// A Web é auxiliar, não mestre: toda edição de Camada (visibilidade/opacidade/bloqueio/ordem/
/// exclusão) vira uma proposta pendente de aprovação de um técnico no Android — ver
/// <see cref="PlantaViewModel.SolicitarOuExecutarAsync"/>. Pede responsável e motivo via os mesmos
/// prompts nativos do navegador já usados em <c>Planta.razor</c> (ex.: confirmar exclusão).
/// </summary>
public sealed class PlataformaEdicaoWeb(IJSRuntime js) : IPlataformaEdicao
{
    public bool ExigeAprovacao => true;

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
