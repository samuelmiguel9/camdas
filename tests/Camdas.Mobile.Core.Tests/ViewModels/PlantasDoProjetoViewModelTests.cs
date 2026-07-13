using Camdas.Contracts;
using Camdas.Domain.Enums;
using Camdas.Mobile.Services;
using Camdas.Mobile.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Camdas.Mobile.Tests.ViewModels;

public class PlantasDoProjetoViewModelTests
{
    private static PlantaDto NovaPlanta(Guid projetoId) =>
        new(Guid.NewGuid(), projetoId, "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow, []);

    [Fact]
    public async Task Carregar_deve_popular_a_lista_de_plantas_do_projeto()
    {
        var projetoId = Guid.NewGuid();
        var apiClient = Substitute.For<IApiClient>();
        apiClient.ListarPlantasDoProjetoAsync(projetoId, Arg.Any<CancellationToken>())
            .Returns(new List<PlantaDto> { NovaPlanta(projetoId), NovaPlanta(projetoId) });

        var viewModel = new PlantasDoProjetoViewModel(apiClient);
        await viewModel.CarregarAsync(projetoId);

        viewModel.Plantas.Should().HaveCount(2);
    }

    [Fact]
    public async Task Importar_deve_adicionar_planta_e_disparar_selecao()
    {
        var projetoId = Guid.NewGuid();
        var plantaImportada = NovaPlanta(projetoId);
        var apiClient = Substitute.For<IApiClient>();
        apiClient.ImportarPlantaAsync(projetoId, "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "a.png", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(plantaImportada);

        var viewModel = new PlantasDoProjetoViewModel(apiClient);
        await viewModel.CarregarAsync(projetoId);

        PlantaDto? plantaRecebida = null;
        viewModel.PlantaSelecionada += (_, planta) => plantaRecebida = planta;

        using var conteudo = new MemoryStream();
        await viewModel.ImportarAsync("Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "a.png", conteudo);

        viewModel.Plantas.Should().Contain(item => item.Planta == plantaImportada);
        plantaRecebida.Should().Be(plantaImportada);
    }
}
