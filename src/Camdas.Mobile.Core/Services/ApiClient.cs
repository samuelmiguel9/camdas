using System.Net.Http.Json;
using Camdas.Contracts;
using Camdas.Domain.Enums;

namespace Camdas.Mobile.Services;

public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public async Task<string> LoginDevAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var resposta = await httpClient.PostAsJsonAsync("api/auth/dev-token", new EmitirTokenRequest(usuarioId), ApiJsonOptions.Padrao, ct);
        resposta.EnsureSuccessStatusCode();
        var corpo = await resposta.Content.ReadFromJsonAsync<EmitirTokenResponse>(ApiJsonOptions.Padrao, ct);
        return corpo!.Token;
    }

    public async Task<ProjetoDto> CriarProjetoAsync(CriarProjetoRequest request, CancellationToken ct = default)
    {
        var resposta = await httpClient.PostAsJsonAsync("api/projetos", request, ApiJsonOptions.Padrao, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProjetoDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<IReadOnlyList<ProjetoDto>> ListarProjetosAsync(CancellationToken ct = default) =>
        (await httpClient.GetFromJsonAsync<List<ProjetoDto>>("api/projetos", ApiJsonOptions.Padrao, ct))!;

    public async Task<ProjetoDto> ObterProjetoAsync(Guid projetoId, CancellationToken ct = default) =>
        (await httpClient.GetFromJsonAsync<ProjetoDto>($"api/projetos/{projetoId}", ApiJsonOptions.Padrao, ct))!;

    public async Task<ProjetoDto> RenomearProjetoAsync(Guid projetoId, string nome, CancellationToken ct = default)
    {
        var resposta = await httpClient.PutAsJsonAsync(
            $"api/projetos/{projetoId}", new RenomearProjetoRequest(nome), ApiJsonOptions.Padrao, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProjetoDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task RemoverProjetoAsync(Guid projetoId, CancellationToken ct = default)
    {
        var resposta = await httpClient.DeleteAsync($"api/projetos/{projetoId}", ct);
        resposta.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<PlantaDto>> ListarPlantasDoProjetoAsync(Guid projetoId, CancellationToken ct = default) =>
        (await httpClient.GetFromJsonAsync<List<PlantaDto>>($"api/projetos/{projetoId}/plantas", ApiJsonOptions.Padrao, ct))!;

    public async Task<PlantaDto> ImportarPlantaAsync(
        Guid projetoId, string nome, string? descricao, string? nomeCliente, TipoArquivoOrigem tipo,
        string nomeArquivo, Stream conteudo, CancellationToken ct = default)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(projetoId.ToString()), ImportarPlantaCampos.ProjetoId },
            { new StringContent(nome), ImportarPlantaCampos.Nome },
            { new StringContent(tipo.ToString()), ImportarPlantaCampos.TipoArquivoOrigem },
            { new StreamContent(conteudo), ImportarPlantaCampos.Arquivo, nomeArquivo },
        };
        if (!string.IsNullOrWhiteSpace(descricao))
            formulario.Add(new StringContent(descricao), ImportarPlantaCampos.Descricao);
        if (!string.IsNullOrWhiteSpace(nomeCliente))
            formulario.Add(new StringContent(nomeCliente), ImportarPlantaCampos.NomeCliente);

        var resposta = await httpClient.PostAsync("api/plantas", formulario, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<PlantaDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<PlantaDto> ObterPlantaAsync(Guid plantaId, CancellationToken ct = default) =>
        (await httpClient.GetFromJsonAsync<PlantaDto>($"api/plantas/{plantaId}", ApiJsonOptions.Padrao, ct))!;

    public async Task<byte[]> ObterArquivoPlantaAsync(Guid plantaId, CancellationToken ct = default) =>
        await httpClient.GetByteArrayAsync($"api/plantas/{plantaId}/arquivo", ct);

    public async Task RemoverPlantaAsync(Guid plantaId, CancellationToken ct = default)
    {
        var resposta = await httpClient.DeleteAsync($"api/plantas/{plantaId}", ct);
        resposta.EnsureSuccessStatusCode();
    }

    public async Task<CamadaDto> CriarCamadaAsync(Guid plantaId, string nome, CancellationToken ct = default)
    {
        var resposta = await httpClient.PostAsJsonAsync(
            $"api/plantas/{plantaId}/camadas", new CriarCamadaRequest(nome), ApiJsonOptions.Padrao, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<CamadaDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task RemoverCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default)
    {
        var resposta = await httpClient.DeleteAsync($"api/plantas/{plantaId}/camadas/{camadaId}", ct);
        resposta.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CamadaDto>> ReordenarCamadasAsync(
        Guid plantaId, IReadOnlyList<Guid> ordemDosIds, CancellationToken ct = default)
    {
        var resposta = await httpClient.PutAsJsonAsync(
            $"api/plantas/{plantaId}/camadas/ordem", new ReordenarCamadasRequest(ordemDosIds), ApiJsonOptions.Padrao, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<List<CamadaDto>>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<CamadaDto> AlternarVisibilidadeCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default)
    {
        var resposta = await httpClient.PatchAsync($"api/plantas/{plantaId}/camadas/{camadaId}/visibilidade", null, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<CamadaDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<CamadaDto> DefinirOpacidadeCamadaAsync(Guid plantaId, Guid camadaId, double opacidade, CancellationToken ct = default)
    {
        var resposta = await httpClient.PatchAsJsonAsync(
            $"api/plantas/{plantaId}/camadas/{camadaId}/opacidade", new DefinirOpacidadeCamadaRequest(opacidade), ApiJsonOptions.Padrao, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<CamadaDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<CamadaDto> BloquearCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default)
    {
        var resposta = await httpClient.PostAsync($"api/plantas/{plantaId}/camadas/{camadaId}/bloqueio", null, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<CamadaDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<CamadaDto> DesbloquearCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default)
    {
        var resposta = await httpClient.DeleteAsync($"api/plantas/{plantaId}/camadas/{camadaId}/bloqueio", ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<CamadaDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<byte[]> ObterImagemCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default) =>
        await httpClient.GetByteArrayAsync($"api/plantas/{plantaId}/camadas/{camadaId}/imagem", ct);

    public async Task<CamadaDto> AtualizarImagemCamadaAsync(Guid plantaId, Guid camadaId, Stream conteudoPng, CancellationToken ct = default)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StreamContent(conteudoPng), "arquivo", "camada.png" },
        };

        var resposta = await httpClient.PutAsync($"api/plantas/{plantaId}/camadas/{camadaId}/imagem", formulario, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<CamadaDto>(ApiJsonOptions.Padrao, ct))!;
    }

    public async Task<IReadOnlyList<HistoricoDto>> ObterHistoricoAsync(Guid plantaId, CancellationToken ct = default) =>
        (await httpClient.GetFromJsonAsync<List<HistoricoDto>>($"api/plantas/{plantaId}/historico", ApiJsonOptions.Padrao, ct))!;
}
