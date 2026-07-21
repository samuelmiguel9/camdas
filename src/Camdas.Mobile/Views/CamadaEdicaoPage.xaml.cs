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

        // Padrão pedido pelo usuário: sem nenhum botão apertado, o toque ajusta a visualização
        // (arrastar/pinça) — só desenha quando o lápis (BotaoDesenhar) está aceso. Antes era o
        // contrário (Canvas.ModoPan = false por padrão, precisava apertar a mão pra mexer na
        // visualização), o que confundia porque um toque acidental já rabiscava a camada. Só afeta
        // esta página — o valor padrão do BindableProperty em si continua false, porque PlantaPage
        // usa o mesmo Canvas com outra semântica (ScrollView próprio + edição inline).
        Canvas.ModoPan = true;
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
    /// pra indicar que está ativo, já que não há cursor/indicador visual do modo no canvas. Ativar
    /// texto sempre desliga o pan (Canvas.ModoPan = false) — senão o toque nunca chegaria em
    /// <see cref="PlantaCanvasView.OnTouch"/>: o Canvas confere ModoPan antes de ModoTexto.</summary>
    private void OnAlternarModoTextoClicked(object? sender, EventArgs e)
    {
        Canvas.ModoTexto = !Canvas.ModoTexto;
        Canvas.ModoPan = !Canvas.ModoTexto;
        AtualizarDestaqueBotao(BotaoTexto, Canvas.ModoTexto);
        AtualizarDestaqueBotao(BotaoDesenhar, false);
    }

    /// <summary>Alterna o "modo desenhar" (lápis): ligado, o toque desenha na camada ativa; desligado
    /// (padrão), o toque ajusta a visualização (arrastar com um dedo — GerenciarPan em
    /// PlantaCanvasView — ou pinça com dois, ver <see cref="OnPinchUpdated"/>). Internamente é só o
    /// inverso de Canvas.ModoPan: desenhar ligado == ModoPan desligado.</summary>
    private void OnAlternarModoDesenharClicked(object? sender, EventArgs e)
    {
        Canvas.ModoPan = !Canvas.ModoPan;
        var desenhoAtivo = !Canvas.ModoPan;
        AtualizarDestaqueBotao(BotaoDesenhar, desenhoAtivo);

        if (desenhoAtivo && Canvas.ModoTexto)
        {
            Canvas.ModoTexto = false;
            AtualizarDestaqueBotao(BotaoTexto, false);
        }
    }

    /// <summary>Zoom por pinça (dois dedos) — pedido do usuário como alternativa ao slider/botões
    /// +/−. Funciona em qualquer momento (mesmo com o lápis aceso): como o PinchGestureRecognizer é
    /// reconhecido pelo MAUI a partir de dois ponteiros simultâneos, e o toque de desenho em
    /// PlantaCanvasView só acompanha o primeiro ponteiro, colocar um segundo dedo na tela naturalmente
    /// para de "desenhar" (o gesto de pinça assume) sem precisar de nenhuma flag extra.</summary>
    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Running && e.Scale > 0)
            AtualizarZoom(Canvas.Zoom * (float)e.Scale);
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
        // sem conseguir voltar a mexer na visualização sem notar que o modo continua ligado. Volta
        // pro padrão (ajuste/pan), não pro desenho — mesmo comportamento de desligar o modo texto
        // manualmente.
        Canvas.ModoTexto = false;
        Canvas.ModoPan = true;
        AtualizarDestaqueBotao(BotaoTexto, false);

        if (string.IsNullOrWhiteSpace(texto))
            return;

        Canvas.AdicionarTexto(texto, ponto, _viewModel.CorTraco, tamanhoFonte: Math.Max(24f, _viewModel.EspessuraTraco * 4));
    }
}
