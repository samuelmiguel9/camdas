using Camdas.Contracts;
using Camdas.Domain.Enums;
using Camdas.Mobile.Services;
using Camdas.Mobile.ViewModels;
using FluentAssertions;
using NSubstitute;
using SkiaSharp;
using Xunit;

namespace Camdas.Mobile.Tests.ViewModels;

public class PlantaViewModelTests
{
    private static CamadaDto NovaCamada(string nome, bool visivel = true, bool bloqueada = false, int ordem = 1, bool temImagemRaster = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), nome, visivel, bloqueada, false, ordem, temImagemRaster, 1.0);

    private static PlantaDto NovaPlanta(params CamadaDto[] camadas) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow, camadas, []);

    private static byte[] PngMinimo()
    {
        using var bitmap = new SKBitmap(1, 1);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }

    private static IApiClient NovoApiClientCom(PlantaDto planta)
    {
        var apiClient = Substitute.For<IApiClient>();
        apiClient.ObterPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(planta);
        apiClient.ObterArquivoPlantaAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(PngMinimo());
        return apiClient;
    }

    [Fact]
    public async Task Carregar_deve_popular_camadas_e_selecionar_a_primeira_como_ativa()
    {
        var camada1 = NovaCamada("Hidráulica");
        var camada2 = NovaCamada("Elétrica");
        var planta = NovaPlanta(camada1, camada2);
        var apiClient = NovoApiClientCom(planta);

        var viewModel = new PlantaViewModel(apiClient, Substitute.For<ISalvadorGaleria>(), new PlataformaEdicaoDireta());
        await viewModel.CarregarAsync(planta.Id);

        viewModel.Camadas.Should().HaveCount(2);
        viewModel.CamadaAtiva.Should().Be(camada1);
        viewModel.MensagemErro.Should().BeNull();
    }

    [Fact]
    public async Task Alternar_visibilidade_deve_substituir_so_a_camada_afetada()
    {
        var camadaHidraulica = NovaCamada("Hidráulica");
        var camadaEletrica = NovaCamada("Elétrica");
        var planta = NovaPlanta(camadaHidraulica, camadaEletrica);
        var apiClient = NovoApiClientCom(planta);

        var camadaEletricaOculta = camadaEletrica with { Visivel = false };
        apiClient.AlternarVisibilidadeCamadaAsync(planta.Id, camadaEletrica.Id, Arg.Any<CancellationToken>())
            .Returns(camadaEletricaOculta);

        var viewModel = new PlantaViewModel(apiClient, Substitute.For<ISalvadorGaleria>(), new PlataformaEdicaoDireta());
        await viewModel.CarregarAsync(planta.Id);

        await viewModel.AlternarVisibilidadeCommand.ExecuteAsync(camadaEletrica);

        viewModel.Camadas.Single(c => c.Nome == "Elétrica").Visivel.Should().BeFalse();
        viewModel.Camadas.Single(c => c.Nome == "Hidráulica").Visivel.Should().BeTrue();
    }

    [Fact]
    public async Task Criar_camada_deve_adiciona_la_e_marcar_como_ativa()
    {
        var camadaExistente = NovaCamada("Hidráulica");
        var planta = NovaPlanta(camadaExistente);
        var apiClient = NovoApiClientCom(planta);
        var camadaNova = NovaCamada("Elétrica");
        apiClient.CriarCamadaAsync(planta.Id, "Elétrica", Arg.Any<CancellationToken>()).Returns(camadaNova);

        var viewModel = new PlantaViewModel(apiClient, Substitute.For<ISalvadorGaleria>(), new PlataformaEdicaoDireta());
        await viewModel.CarregarAsync(planta.Id);

        await viewModel.CriarCamadaAsync("Elétrica");

        viewModel.Camadas.Should().Contain(camadaNova);
        viewModel.CamadaAtiva.Should().Be(camadaNova);
    }

    [Fact]
    public async Task Selecionar_camada_deve_disparar_evento_de_navegacao_para_edicao()
    {
        var camada = NovaCamada("Hidráulica");
        var planta = NovaPlanta(camada);
        var apiClient = NovoApiClientCom(planta);

        var viewModel = new PlantaViewModel(apiClient, Substitute.For<ISalvadorGaleria>(), new PlataformaEdicaoDireta());
        await viewModel.CarregarAsync(planta.Id);

        CamadaDto? camadaRecebida = null;
        viewModel.CamadaSelecionadaParaEdicao += (_, c) => camadaRecebida = c;

        viewModel.SelecionarCamadaCommand.Execute(camada);

        camadaRecebida.Should().Be(camada);
        viewModel.CamadaAtiva.Should().Be(camada);
    }

    [Fact]
    public async Task ReordenarArrastando_deve_mover_a_camada_para_a_posicao_da_vizinha_e_recarregar_com_o_resultado_do_servidor()
    {
        var camada1 = NovaCamada("Primeira", ordem: 1);
        var camada2 = NovaCamada("Segunda", ordem: 2);
        var camada3 = NovaCamada("Terceira", ordem: 3);
        var planta = NovaPlanta(camada1, camada2, camada3);
        var apiClient = NovoApiClientCom(planta);

        var camada2Reordenada = camada2 with { Ordem = 3 };
        var camada3Reordenada = camada3 with { Ordem = 2 };
        apiClient.ReordenarCamadasAsync(planta.Id, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([camada1, camada3Reordenada, camada2Reordenada]);

        var viewModel = new PlantaViewModel(apiClient, Substitute.For<ISalvadorGaleria>(), new PlataformaEdicaoDireta());
        await viewModel.CarregarAsync(planta.Id);

        // Arrasta a "Terceira" pra posição da "Segunda".
        await viewModel.ReordenarArrastandoAsync(camada3, camada2);

        await apiClient.Received(1).ReordenarCamadasAsync(
            planta.Id, Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { camada1.Id, camada3.Id, camada2.Id })),
            Arg.Any<CancellationToken>());
        viewModel.Camadas.Select(c => c.Id).Should().Equal(camada1.Id, camada3.Id, camada2.Id);
    }
}
