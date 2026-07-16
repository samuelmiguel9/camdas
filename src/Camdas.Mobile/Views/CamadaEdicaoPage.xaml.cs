using Camdas.Mobile.ViewModels;
using SkiaSharp;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
[QueryProperty(nameof(CamadaId), "camadaId")]
public partial class CamadaEdicaoPage : ContentPage
{
    private readonly CamadaEdicaoViewModel _viewModel;
    private bool _zoomAjustadoNoCarregamento;

    public string PlantaId { get; set; } = string.Empty;
    public string CamadaId { get; set; } = string.Empty;

    public CamadaEdicaoPage(CamadaEdicaoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.CamadaSalva += async (_, _) => await Shell.Current.GoToAsync("..");
        Canvas.SolicitarTexto += OnCanvasSolicitarTexto;
        // Auto-salva um rascunho local a cada alteração (traço solto, texto, desfazer/refazer,
        // limpar) — não perde o trabalho se o app fechar antes de "Salvar camada" mandar pro servidor.
        Canvas.DesenhoAlterado += async (_, _) => await _viewModel.SalvarRascunhoAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(PlantaId, out var plantaId) && Guid.TryParse(CamadaId, out var camadaId))
            await _viewModel.CarregarAsync(plantaId, camadaId);

        // Primeiro carregamento: ajusta o zoom pra caber na tela (planta costuma ser maior que o
        // aparelho) — o traço continua sendo gravado na resolução nativa da imagem base
        // independente do zoom, ver comentário em PlantaCanvasView.UsarResolucaoNativa.
        if (!_zoomAjustadoNoCarregamento && _viewModel.ImagemBase is not null)
        {
            _zoomAjustadoNoCarregamento = true;
            AjustarZoomParaTela();
        }
    }

    private void AtualizarZoom(float zoom)
    {
        if (_viewModel.ImagemBase is null)
            return;

        zoom = Math.Clamp(zoom, (float)ZoomSlider.Minimum, (float)ZoomSlider.Maximum);

        // Reseta o pan a cada mudança de zoom — sem isso, arrastar demais a planta (PanX/PanY) e
        // depois mudar o zoom pode deixar a imagem fora da área visível sem um jeito óbvio de achar
        // o caminho de volta. Mudar o zoom sempre recentraliza.
        Canvas.PanX = 0;
        Canvas.PanY = 0;
        Canvas.UsarResolucaoNativa = true;
        Canvas.Zoom = zoom;
        // Sem ScrollView (removido — competia com o gesto de desenhar), o Canvas fica com tamanho
        // fixo (preenche a célula do Grid) e o zoom só afeta o Translate/Scale internos do
        // OnPaintSurface — diferente da tela de visualização (PlantaPage), que ainda tem ScrollView
        // e por isso pode crescer o WidthRequest/HeightRequest pra rolar. Crescer aqui faria a
        // planta "vazar" pra fora do layout, já que nada mais contém/corta esse excesso.
        Canvas.AtualizarPreview();

        ZoomSlider.Value = zoom;
        ZoomLabel.Text = $"{(int)Math.Round(zoom * 100)}%";
    }

    private void AjustarZoomParaTela()
    {
        if (_viewModel.ImagemBase is not { } imagemBase || imagemBase.Width <= 0)
            return;

        var larguraDisponivel = Width > 0 ? Width - 32 : imagemBase.Width;
        var zoomAjustado = Math.Min(1f, (float)(larguraDisponivel / imagemBase.Width));
        AtualizarZoom(zoomAjustado);
    }

    private void OnZoomSliderChanged(object? sender, ValueChangedEventArgs e) => AtualizarZoom((float)e.NewValue);

    private void OnAumentarZoomClicked(object? sender, EventArgs e) => AtualizarZoom(Canvas.Zoom + 0.25f);

    private void OnDiminuirZoomClicked(object? sender, EventArgs e) => AtualizarZoom(Canvas.Zoom - 0.25f);

    private void OnCorClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string cor })
            _viewModel.EscolherCorCommand.Execute(cor);
    }

    private void OnLimparClicked(object? sender, EventArgs e) => Canvas.LimparCamadaAtiva();

    private void OnDesfazerClicked(object? sender, EventArgs e) => Canvas.DesfazerUltimaAcao();

    private void OnRefazerClicked(object? sender, EventArgs e) => Canvas.RefazerAcao();

    /// <summary>Alterna o "modo texto": enquanto ligado, tocar no canvas não desenha, dispara
    /// <see cref="PlantaCanvasView.SolicitarTexto"/> — o próprio botão fica destacado (fundo escuro)
    /// pra indicar que está ativo, já que não há cursor/indicador visual do modo no canvas.</summary>
    private void OnAlternarModoTextoClicked(object? sender, EventArgs e)
    {
        Canvas.ModoTexto = !Canvas.ModoTexto;
        AtualizarDestaqueBotao(BotaoTexto, Canvas.ModoTexto);

        // Texto e pan são mutuamente exclusivos com o desenho — ligar um desliga o outro, senão os
        // dois "modos especiais" ficam competindo pelo mesmo toque.
        if (Canvas.ModoTexto && Canvas.ModoPan)
        {
            Canvas.ModoPan = false;
            AtualizarDestaqueBotao(BotaoPan, false);
        }
    }

    /// <summary>Alterna o "modo pan": enquanto ligado, arrastar o dedo move a visualização
    /// (Canvas.PanX/PanY) em vez de desenhar — separa por completo o gesto de ajustar zoom/posição do
    /// gesto de desenhar, sem depender de heurística de "quantos dedos" nem de ScrollView.</summary>
    private void OnAlternarModoPanClicked(object? sender, EventArgs e)
    {
        Canvas.ModoPan = !Canvas.ModoPan;
        AtualizarDestaqueBotao(BotaoPan, Canvas.ModoPan);

        if (Canvas.ModoPan && Canvas.ModoTexto)
        {
            Canvas.ModoTexto = false;
            AtualizarDestaqueBotao(BotaoTexto, false);
        }
    }

    private static void AtualizarDestaqueBotao(Button botao, bool ativo)
    {
        botao.BackgroundColor = ativo ? Color.FromArgb("#333") : Colors.Transparent;
        botao.TextColor = ativo ? Colors.White : Color.FromArgb("#333");
    }

    private async void OnCanvasSolicitarTexto(object? sender, SKPoint ponto)
    {
        var texto = await DisplayPromptAsync("Adicionar texto", "Escreva o texto:");

        // Sai do modo texto depois de um toque, mesmo se cancelar — evita o usuário ficar "preso"
        // sem conseguir voltar a desenhar sem notar que o modo continua ligado.
        Canvas.ModoTexto = false;
        AtualizarDestaqueBotao(BotaoTexto, false);

        if (string.IsNullOrWhiteSpace(texto))
            return;

        Canvas.AdicionarTexto(texto, ponto, _viewModel.CorTraco, tamanhoFonte: Math.Max(24f, _viewModel.EspessuraTraco * 4));
    }
}
