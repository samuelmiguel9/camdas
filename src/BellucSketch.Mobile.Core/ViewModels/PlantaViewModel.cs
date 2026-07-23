using System.Collections.ObjectModel;
using System.Text.Json;
using BellucSketch.Contracts;
using BellucSketch.Domain.Enums;
using BellucSketch.Mobile.Exportacao;
using BellucSketch.Mobile.Rendering;
using BellucSketch.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace BellucSketch.Mobile.ViewModels;

/// <summary>
/// Tela principal da planta: mostra a composição de todas as camadas visíveis e gerencia
/// visibilidade/bloqueio/criação/prioridade de camadas. O desenho também acontece aqui, sob demanda:
/// <see cref="ModoEdicao"/> liga as ferramentas e passa a desenhar na <see cref="CamadaAtiva"/>,
/// mantendo as demais camadas visíveis por baixo (o usuário se guia por elas pra não repetir medida).
///
/// Compartilhada entre Android (mestre) e Web (auxiliar): <paramref name="plataformaEdicao"/> decide,
/// por tipo de operação, se a edição aplica direto (sempre no Android) ou vira uma
/// <see cref="EdicaoPendenteDto"/> aguardando aprovação de um técnico — na Web só "Excluir" passa por
/// aprovação (apaga o traço junto); visibilidade/opacidade/bloqueio/ordem são livres, por não serem
/// uma alteração crítica — ver <see cref="SolicitarOuExecutarAsync"/>.
/// </summary>
public partial class PlantaViewModel(IApiClient apiClient, ISalvadorGaleria salvadorGaleria, IPlataformaEdicao plataformaEdicao) : BaseViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoAuditoria))]
    private PlantaDto? _planta;

    /// <summary>"Criado em .../por ... · Modificado em .../por ..." — resumo pronto pra exibir (ver
    /// PlantaPage.xaml e Web/Pages/Planta.razor), montado a partir do que ObterPlantaQueryHandler já
    /// resolve (histórico de auditoria). Omite "Modificado" quando nada aconteceu depois da criação
    /// (mesmo instante/autor). Vazio enquanto a planta ainda não carregou.</summary>
    public string ResumoAuditoria
    {
        get
        {
            if (Planta is not { } planta)
                return string.Empty;

            var criado = $"Criado em {planta.DataImportacao:dd/MM/yyyy HH:mm}" +
                (planta.NomeCriador is { } criador ? $" por {criador}" : string.Empty);

            if (planta.UltimaModificacaoEm is not { } modificadoEm || modificadoEm <= planta.DataImportacao)
                return criado;

            var modificado = $"Modificado em {modificadoEm:dd/MM/yyyy HH:mm}" +
                (planta.NomeUltimoModificador is { } modificador ? $" por {modificador}" : string.Empty);
            return $"{criado} · {modificado}";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CamadaEmEdicaoId))]
    [NotifyPropertyChangedFor(nameof(PermiteEscolherCor))]
    private CamadaDto? _camadaAtiva;

    /// <summary>Camadas pré-definidas (ver <see cref="EditarCamada"/> e PlantaPage.OnNovaCamadaClicked)
    /// identificadas pelo nome — travadas numa cor fixa (hidráulica = azul, elétrica = amarelo), pra
    /// manter a convenção entre plantas sem depender do usuário lembrar/escolher certo toda vez.</summary>
    public const string NomeCamadaHidraulica = "Hidráulica";
    public const string NomeCamadaEletrica = "Elétrica";
    public const string CorHidraulica = "#2E86DE";
    public const string CorEletrica = "#F1C40F";

    /// <summary>Camada especial que guarda os ícones técnicos padronizados (ferramenta de ícones) —
    /// criada automaticamente (ver <see cref="CarregarAsync"/>) se ainda não existir, e nunca pode
    /// ser ocultada (ver <see cref="AlternarVisibilidadeAsync"/>): a ideia é o ícone nunca sumir da
    /// composição, não importa o que o usuário fizer com as demais camadas.</summary>
    public const string NomeCamadaIcones = "Ícones";

    /// <summary>Falso pras camadas pré-definidas (hidráulica/elétrica) — a paleta de cores fica
    /// escondida na barra de ferramentas e o traço sempre sai na cor fixa daquela camada.</summary>
    public bool PermiteEscolherCor => CamadaAtiva is not { Nome: NomeCamadaHidraulica or NomeCamadaEletrica };

    /// <summary>Id da camada que o canvas deve tratar como ativa pra desenho — só preenchido em
    /// <see cref="ModoEdicao"/>. Fora da edição fica null, então tocar na planta na visualização
    /// normal não risca nada.</summary>
    public Guid? CamadaEmEdicaoId => ModoEdicao ? CamadaAtiva?.Id : null;

    [ObservableProperty]
    private SKBitmap? _imagemBase;

    /// <summary>Camada com o menu de opções (opacidade/limpar/bloqueios/duplicar/excluir) aberto — null quando fechado.</summary>
    [ObservableProperty]
    private CamadaDto? _camadaMenuAberta;

    /// <summary>Quando true, a tela de visualização entra em modo de desenho da <see cref="CamadaAtiva"/>
    /// (as ferramentas aparecem e o toque desenha) — as demais camadas continuam visíveis por baixo,
    /// pra o usuário se guiar por elas e evitar medidas redundantes. Ver PlantaPage.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CamadaEmEdicaoId))]
    private bool _modoEdicao;

    [ObservableProperty]
    private string _corTraco = "#000000";

    [ObservableProperty]
    private float _espessuraTraco = 3f;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EspessuraMaxima))]
    private bool _modoApagar;

    /// <summary>A borracha usa uma escala bem maior que o traço — apagar uma área grande de uma vez
    /// exige uma espessura muito acima do que faz sentido pra desenhar linhas finas.</summary>
    public float EspessuraMaxima => ModoApagar ? 120f : 24f;

    partial void OnModoApagarChanged(bool value)
    {
        if (EspessuraTraco > EspessuraMaxima)
            EspessuraTraco = EspessuraMaxima;
    }

    public ObservableCollection<CamadaDto> Camadas { get; } = [];

    /// <summary>
    /// Bitmap raster de cada camada, por Id. A mesma instância de Dictionary é compartilhada com
    /// PlantaCanvasView (via binding) e mutada por ela a cada traço — esta classe só lê/grava do
    /// zero ao carregar/salvar.
    /// </summary>
    public Dictionary<Guid, SKBitmap> ImagensPorCamada { get; } = [];

    public async Task CarregarAsync(Guid plantaId)
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var camadaAtivaAnteriorId = CamadaAtiva?.Id;

            Planta = await apiClient.ObterPlantaAsync(plantaId);
            Camadas.Clear();
            foreach (var camada in Planta.Camadas)
                Camadas.Add(camada);

            await GarantirCamadaIconesAsync();

            CamadaAtiva = Camadas.FirstOrDefault(c => c.Id == camadaAtivaAnteriorId) ?? Camadas.FirstOrDefault();

            await CarregarImagensAsync();
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível carregar a planta: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Garante que a camada "Ícones" sempre existe pra essa planta — cria uma vez, na
    /// primeira vez que a planta é aberta sem ela (plantas antigas, criadas antes dessa ferramenta
    /// existir). Não usa <see cref="CriarCamadaAsync"/> de propósito: aquele também troca
    /// <see cref="CamadaAtiva"/>, e abrir uma planta nunca deve trocar a camada selecionada sozinho.</summary>
    private async Task GarantirCamadaIconesAsync()
    {
        if (Planta is null || Camadas.Any(c => c.Nome == NomeCamadaIcones))
            return;

        var camada = await apiClient.CriarCamadaAsync(Planta.Id, NomeCamadaIcones);
        Camadas.Add(camada);
    }

    private async Task CarregarImagensAsync()
    {
        if (Planta is null)
            return;

        ImagemBase?.Dispose();
        var bytesBase = await apiClient.ObterArquivoPlantaAsync(Planta.Id);
        ImagemBase = BitmapDecodificacao.DecodificarLimitado(bytesBase);

        foreach (var bitmap in ImagensPorCamada.Values)
            bitmap.Dispose();
        ImagensPorCamada.Clear();

        foreach (var camada in Camadas.Where(c => c.TemImagemRaster))
        {
            var bytesCamada = await apiClient.ObterImagemCamadaAsync(Planta.Id, camada.Id);
            var bitmap = BitmapDecodificacao.DecodificarLimitado(bytesCamada);
            if (bitmap is not null)
                ImagensPorCamada[camada.Id] = bitmap;
        }
    }

    public async Task CriarCamadaAsync(string nome)
    {
        if (Planta is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var camada = await apiClient.CriarCamadaAsync(Planta.Id, nome);
            Camadas.Add(camada);
            CamadaAtiva = camada;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível criar a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Compõe a imagem base + todas as camadas visíveis (mesma lógica de desenho da tela)
    /// num PNG só e salva na galeria do aparelho — "Salvar tudo".</summary>
    public async Task SalvarComposicaoNaGaleriaAsync()
    {
        if (Planta is null || ImagemBase is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            using var bitmap = new SKBitmap(ImagemBase.Width, ImagemBase.Height);
            using (var canvas = new SKCanvas(bitmap))
                PlantaOverlayRenderer.Desenhar(canvas, Camadas, ImagemBase, ImagensPorCamada);

            // Encode direto do SKBitmap (não via SKImage.FromBitmap) — o Galaxy Tab A crashava com
            // SIGSEGV nativo dentro de sk_image_new_from_bitmap (libSkiaSharp.so), reproduzido em
            // mais de uma tela que fazia esse wrap antes de codificar o PNG.
            using var dados = bitmap.Encode(SKEncodedImageFormat.Png, 100);

            var nomeArquivo = $"{Planta.Nome}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            await salvadorGaleria.SalvarPngAsync(dados.ToArray(), nomeArquivo);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível salvar na galeria: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Gera o arquivo de projeto (JSON — ver <see cref="ArquivoPlantaService"/>) com a
    /// imagem base + o traço de todas as camadas, pra enviar a outro dispositivo e reabrir lá como
    /// planta nova (ver PlantasDoProjetoViewModel.ImportarArquivoDeProjetoAsync). Retorna null (e
    /// preenche MensagemErro) se não houver planta carregada ainda.</summary>
    public byte[]? ExportarParaArquivo()
    {
        if (Planta is null || ImagemBase is null)
        {
            MensagemErro = "Nada pra exportar ainda — a planta não terminou de carregar.";
            return null;
        }

        try
        {
            return ArquivoPlantaService.Exportar(Planta, ImagemBase, ImagensPorCamada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível exportar o projeto: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Move a camada arrastada (<paramref name="origem"/>) pra posição da camada onde ela foi solta
    /// (<paramref name="destino"/>) e persiste a nova ordem no servidor, que devolve a lista já
    /// renumerada em ordem crescente.
    /// </summary>
    public async Task ReordenarArrastandoAsync(CamadaDto origem, CamadaDto destino)
    {
        if (Planta is null)
            return;

        var indiceOrigem = Camadas.IndexOf(origem);
        var indiceDestino = Camadas.IndexOf(destino);
        if (indiceOrigem < 0 || indiceDestino < 0 || indiceOrigem == indiceDestino)
            return;

        var ids = Camadas.Select(c => c.Id).ToList();
        var idOrigem = ids[indiceOrigem];
        ids.RemoveAt(indiceOrigem);
        ids.Insert(indiceDestino, idOrigem);

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            if (await SolicitarOuExecutarAsync(camadaId: null, TipoOperacaoEdicaoPendente.Reordenar, JsonSerializer.Serialize(new { ordemDosIds = ids })))
                return;

            var atualizadas = await apiClient.ReordenarCamadasAsync(Planta.Id, ids);
            var camadaAtivaId = CamadaAtiva?.Id;
            Camadas.Clear();
            foreach (var c in atualizadas)
                Camadas.Add(c);
            CamadaAtiva = Camadas.FirstOrDefault(c => c.Id == camadaAtivaId);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível reordenar as camadas: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    [RelayCommand]
    private void AbrirMenuCamada(CamadaDto camada) => CamadaMenuAberta = camada;

    [RelayCommand]
    private void FecharMenuCamada() => CamadaMenuAberta = null;

    [RelayCommand]
    private void SelecionarCamada(CamadaDto camada) => EditarCamada(camada);

    /// <summary>Entra no modo de edição inline da camada indicada — desenho acontece na própria tela
    /// de visualização, com as demais camadas visíveis por baixo (evita medida redundante).</summary>
    public void EditarCamada(CamadaDto camada)
    {
        CamadaAtiva = camada;
        ModoApagar = false;
        CamadaMenuAberta = null;
        ModoEdicao = true;

        CorTraco = camada.Nome switch
        {
            NomeCamadaHidraulica => CorHidraulica,
            NomeCamadaEletrica => CorEletrica,
            _ => CorTraco,
        };
    }

    public void SairDaEdicao() => ModoEdicao = false;

    /// <summary>Entra no modo de edição sem uma camada específica em mente — usado pela aba
    /// "Edição" da barra de ações da planta (Web), que troca pro modo de desenho antes do usuário
    /// escolher a camada no painel lateral. Reaproveita a última camada ativa, se ainda existir e não
    /// estiver bloqueada; senão cai na primeira camada desbloqueada da lista.</summary>
    public void EntrarEmModoEdicao()
    {
        var alvo = CamadaAtiva is { Bloqueada: false } atual && Camadas.Any(c => c.Id == atual.Id)
            ? atual
            : Camadas.FirstOrDefault(c => !c.Bloqueada);

        if (alvo is not null)
            EditarCamada(alvo);
    }

    /// <summary>Exclui a planta inteira (e todas as suas camadas) — sem aprovação, mesma regra das
    /// demais operações "livres" na Web (só "excluir CAMADA" passa por aprovação, ver
    /// SolicitarOuExecutarAsync). Quem chama decide pra onde navegar depois.</summary>
    public async Task ExcluirPlantaAsync()
    {
        if (Planta is null)
            return;

        await apiClient.RemoverPlantaAsync(Planta.Id);
    }

    /// <summary>Salva o traço da camada ativa no servidor. Encode direto do SKBitmap (não via
    /// SKImage.FromBitmap) — ver comentário em <see cref="SalvarComposicaoNaGaleriaAsync"/> sobre o
    /// crash nativo. Retorna false se nada foi desenhado ainda (bitmap inexistente).</summary>
    public async Task<bool> SalvarCamadaAtivaAsync()
    {
        if (Planta is null || CamadaAtiva is not { } camada || !ImagensPorCamada.TryGetValue(camada.Id, out var bitmap))
            return false;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            using var dados = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = dados.AsStream();
            var atualizada = await apiClient.AtualizarImagemCamadaAsync(Planta.Id, camada.Id, stream);
            SubstituirCamada(atualizada);
            return true;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível salvar a camada: {ex.Message}";
            return false;
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    [RelayCommand]
    private async Task AlternarVisibilidadeAsync(CamadaDto camada)
    {
        // A camada "Ícones" nunca pode ser ocultada (ver NomeCamadaIcones) — no-op silencioso em vez
        // de erro, já que o botão de olho dela fica desabilitado na tela (ver PlantaPage.xaml), mas
        // vale a defesa aqui também caso algo dispare o comando por outro caminho.
        if (Planta is null || camada.Nome == NomeCamadaIcones)
            return;

        try
        {
            if (await SolicitarOuExecutarAsync(camada.Id, TipoOperacaoEdicaoPendente.AlternarVisibilidade, "{}"))
                return;

            var atualizada = await apiClient.AlternarVisibilidadeCamadaAsync(Planta.Id, camada.Id);
            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar a visibilidade: {ex.Message}";
        }
    }

    /// <summary>Chamado ao soltar o slider de opacidade (não a cada tick) — evita spam de requisições.</summary>
    public async Task DefinirOpacidadeAsync(CamadaDto camada, double opacidade)
    {
        if (Planta is null)
            return;

        try
        {
            if (await SolicitarOuExecutarAsync(camada.Id, TipoOperacaoEdicaoPendente.DefinirOpacidade, JsonSerializer.Serialize(new { opacidade })))
                return;

            var atualizada = await apiClient.DefinirOpacidadeCamadaAsync(Planta.Id, camada.Id, opacidade);
            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar a opacidade: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AlternarBloqueioAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        try
        {
            if (await SolicitarOuExecutarAsync(camada.Id, TipoOperacaoEdicaoPendente.AlternarBloqueio, "{}"))
                return;

            var atualizada = camada.Bloqueada
                ? await apiClient.DesbloquearCamadaAsync(Planta.Id, camada.Id)
                : await apiClient.BloquearCamadaAsync(Planta.Id, camada.Id);

            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar o bloqueio: {ex.Message}";
        }
    }

    public async Task RemoverCamadaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            if (await SolicitarOuExecutarAsync(camada.Id, TipoOperacaoEdicaoPendente.Excluir, "{}"))
                return;

            await apiClient.RemoverCamadaAsync(Planta.Id, camada.Id);
            Camadas.Remove(camada);
            ImagensPorCamada.Remove(camada.Id, out var bitmap);
            bitmap?.Dispose();

            if (CamadaAtiva?.Id == camada.Id)
                CamadaAtiva = Camadas.FirstOrDefault();
            if (CamadaMenuAberta?.Id == camada.Id)
                CamadaMenuAberta = null;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível excluir a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Esvazia o traço da camada (fica em branco) sem excluí-la — usado pelo menu de opções da camada.</summary>
    [RelayCommand]
    private async Task LimparCamadaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        try
        {
            var atualizada = await apiClient.LimparCamadaAsync(Planta.Id, camada.Id);
            SubstituirCamada(atualizada);
            if (ImagensPorCamada.Remove(camada.Id, out var bitmap))
                bitmap.Dispose();
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível limpar a camada: {ex.Message}";
        }
    }

    /// <summary>Cria uma cópia da camada (nome, opacidade, visibilidade e traço) logo abaixo dela.</summary>
    [RelayCommand]
    private async Task DuplicarCamadaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var copia = await apiClient.DuplicarCamadaAsync(Planta.Id, camada.Id);
            Camadas.Add(copia);
            if (copia.TemImagemRaster)
                ImagensPorCamada[copia.Id] = SKBitmap.Decode(await apiClient.ObterImagemCamadaAsync(Planta.Id, copia.Id));

            CamadaMenuAberta = null;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível duplicar a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Bloqueio de opacidade/alpha — impede alterar a transparência do traço já pintado.</summary>
    [RelayCommand]
    private async Task AlternarBloqueioAlphaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        try
        {
            var atualizada = camada.BloqueioAlpha
                ? await apiClient.DesbloquearAlphaCamadaAsync(Planta.Id, camada.Id)
                : await apiClient.BloquearAlphaCamadaAsync(Planta.Id, camada.Id);

            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar o bloqueio de opacidade: {ex.Message}";
        }
    }

    /// <summary>
    /// Quando <see cref="IPlataformaEdicao.PrecisaAprovacao"/> retorna true pra este tipo de operação
    /// (Web, só em "Excluir" hoje — visibilidade/opacidade/bloqueio/ordem são livres), registra a
    /// mudança como pendente em vez de deixar o chamador aplicá-la direto, e retorna true (chamador
    /// deve parar). No Android retorna false direto, sem pedir nada — chamador segue com a execução
    /// normal.
    /// </summary>
    private async Task<bool> SolicitarOuExecutarAsync(Guid? camadaId, TipoOperacaoEdicaoPendente tipoOperacao, string dadosDepoisJson)
    {
        if (!plataformaEdicao.PrecisaAprovacao(tipoOperacao))
            return false;

        var justificativa = await plataformaEdicao.PedirJustificativaAsync();
        if (justificativa is null)
            return true;

        await apiClient.SolicitarEdicaoCamadaAsync(
            Planta!.Id, camadaId, tipoOperacao, dadosDepoisJson, justificativa.Value.Responsavel, justificativa.Value.Motivo);

        Planta = await apiClient.ObterPlantaAsync(Planta.Id);
        return true;
    }

    private void SubstituirCamada(CamadaDto atualizada)
    {
        for (var i = 0; i < Camadas.Count; i++)
        {
            if (Camadas[i].Id == atualizada.Id)
            {
                Camadas[i] = atualizada;
                break;
            }
        }

        if (CamadaAtiva?.Id == atualizada.Id)
            CamadaAtiva = atualizada;
        if (CamadaMenuAberta?.Id == atualizada.Id)
            CamadaMenuAberta = atualizada;
    }
}
