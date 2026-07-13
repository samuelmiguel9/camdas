using Camdas.Contracts;
using Camdas.Domain.Enums;
using Camdas.Mobile.Services;
using Camdas.Mobile.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Camdas.Mobile.Tests.ViewModels;

public class HistoricoViewModelTests
{
    [Fact]
    public async Task Carregar_deve_popular_historico()
    {
        var plantaId = Guid.NewGuid();
        var apiClient = Substitute.For<IApiClient>();
        apiClient.ObterHistoricoAsync(plantaId, Arg.Any<CancellationToken>()).Returns(new List<HistoricoDto>
        {
            new(Guid.NewGuid(), "Camada", Guid.NewGuid(), plantaId, TipoAcaoHistorico.CamadaAdicionada, Guid.NewGuid(), DateTime.UtcNow, null, null),
        });

        var viewModel = new HistoricoViewModel(apiClient);
        await viewModel.CarregarAsync(plantaId);

        viewModel.Itens.Should().ContainSingle();
        viewModel.MensagemErro.Should().BeNull();
    }
}
