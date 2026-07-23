using BellucSketch.Contracts;
using BellucSketch.Domain.Enums;
using BellucSketch.Mobile.Services;
using BellucSketch.Mobile.ViewModels;
using FluentAssertions;
using NSubstitute;
using SkiaSharp;
using Xunit;

namespace BellucSketch.Mobile.Tests.ViewModels;

public class CamadaEdicaoViewModelTests
{
    private static CamadaDto NovaCamada(bool temImagemRaster = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Hidráulica", true, false, false, 1, temImagemRaster, 1.0);

    private static byte[] PngMinimo()
    {
        using var bitmap = new SKBitmap(1, 1);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }

    [Fact]
    public async Task Carregar_deve_popular_camada_unica_e_imagem_base()
    {
        var camada = NovaCamada();
        var planta = new PlantaDto(Guid.NewGuid(), Guid.NewGuid(), "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow, [camada], []);

        var apiClient = Substitute.For<IApiClient>();
        apiClient.ObterPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(planta);
        apiClient.ObterArquivoPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(PngMinimo());

        var viewModel = new CamadaEdicaoViewModel(apiClient, Substitute.For<IArmazenamentoRascunho>());
        await viewModel.CarregarAsync(planta.Id, camada.Id);

        viewModel.Camada.Should().Be(camada);
        viewModel.Camadas.Should().ContainSingle(c => c.Id == camada.Id);
        viewModel.ImagemBase.Should().NotBeNull();
        viewModel.MensagemErro.Should().BeNull();
    }

    [Fact]
    public async Task Salvar_deve_enviar_o_bitmap_e_disparar_evento_de_camada_salva()
    {
        var camada = NovaCamada();
        var planta = new PlantaDto(Guid.NewGuid(), Guid.NewGuid(), "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow, [camada], []);

        var apiClient = Substitute.For<IApiClient>();
        apiClient.ObterPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(planta);
        apiClient.ObterArquivoPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(PngMinimo());
        apiClient.AtualizarImagemCamadaAsync(planta.Id, camada.Id, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(camada with { TemImagemRaster = true });

        var viewModel = new CamadaEdicaoViewModel(apiClient, Substitute.For<IArmazenamentoRascunho>());
        await viewModel.CarregarAsync(planta.Id, camada.Id);
        viewModel.ImagensPorCamada[camada.Id] = new SKBitmap(4, 4);

        var disparado = false;
        viewModel.CamadaSalva += (_, _) => disparado = true;

        await viewModel.SalvarCommand.ExecuteAsync(null);

        await apiClient.Received(1).AtualizarImagemCamadaAsync(planta.Id, camada.Id, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        disparado.Should().BeTrue();
    }

    [Fact]
    public async Task Salvar_sem_nada_desenhado_deve_mostrar_mensagem_e_nao_chamar_api()
    {
        var camada = NovaCamada();
        var planta = new PlantaDto(Guid.NewGuid(), Guid.NewGuid(), "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow, [camada], []);

        var apiClient = Substitute.For<IApiClient>();
        apiClient.ObterPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(planta);
        apiClient.ObterArquivoPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(PngMinimo());

        var viewModel = new CamadaEdicaoViewModel(apiClient, Substitute.For<IArmazenamentoRascunho>());
        await viewModel.CarregarAsync(planta.Id, camada.Id);

        await viewModel.SalvarCommand.ExecuteAsync(null);

        viewModel.MensagemErro.Should().NotBeNull();
        await apiClient.DidNotReceive().AtualizarImagemCamadaAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }
}
