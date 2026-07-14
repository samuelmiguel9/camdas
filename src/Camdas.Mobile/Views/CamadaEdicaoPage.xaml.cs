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
        if (_viewModel.ImagemBase is not { } imagemBase)
            return;

        zoom = Math.Clamp(zoom, (float)ZoomSlider.Minimum, (float)ZoomSlider.Maximum);

        Canvas.UsarResolucaoNativa = true;
        Canvas.Zoom = zoom;
        Canvas.WidthRequest = imagemBase.Width * zoom;
        Canvas.HeightRequest = imagemBase.Height * zoom;
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
        BotaoTexto.BackgroundColor = Canvas.ModoTexto ? Color.FromArgb("#333") : Colors.Transparent;
        BotaoTexto.TextColor = Canvas.ModoTexto ? Colors.White : Color.FromArgb("#333");
    }

    private async void OnCanvasSolicitarTexto(object? sender, SKPoint ponto)
    {
        var texto = await DisplayPromptAsync("Adicionar texto", "Escreva o texto:");

        // Sai do modo texto depois de um toque, mesmo se cancelar — evita o usuário ficar "preso"
        // sem conseguir voltar a desenhar sem notar que o modo continua ligado.
        Canvas.ModoTexto = false;
        BotaoTexto.BackgroundColor = Colors.Transparent;
        BotaoTexto.TextColor = Color.FromArgb("#333");

        if (string.IsNullOrWhiteSpace(texto))
            return;

        Canvas.AdicionarTexto(texto, ponto, _viewModel.CorTraco, tamanhoFonte: Math.Max(24f, _viewModel.EspessuraTraco * 4));
    }
}
