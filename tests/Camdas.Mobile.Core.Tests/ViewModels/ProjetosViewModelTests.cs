using Camdas.Contracts;
using Camdas.Domain.Enums;
using Camdas.Mobile.Services;
using Camdas.Mobile.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Camdas.Mobile.Tests.ViewModels;

public class ProjetosViewModelTests
{
    private static ProjetoDto NovoProjeto(string nome) =>
        new(Guid.NewGuid(), nome, null, Guid.NewGuid(), DateTime.UtcNow, StatusProjeto.Ativo);

    [Fact]
    public async Task Carregar_deve_popular_a_colecao_de_projetos()
    {
        var apiClient = Substitute.For<IApiClient>();
        apiClient.ListarProjetosAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjetoDto> { NovoProjeto("Residência Alfa"), NovoProjeto("Residência Beta") });

        var viewModel = new ProjetosViewModel(apiClient);

        await viewModel.CarregarCommand.ExecuteAsync(null);

        viewModel.Projetos.Should().HaveCount(2);
        viewModel.EstaCarregando.Should().BeFalse();
    }

    [Fact]
    public async Task Criar_projeto_deve_adicionar_na_colecao_e_limpar_o_campo()
    {
        var apiClient = Substitute.For<IApiClient>();
        apiClient.CriarProjetoAsync(Arg.Any<CriarProjetoRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(NovoProjeto(callInfo.Arg<CriarProjetoRequest>().Nome)));

        var viewModel = new ProjetosViewModel(apiClient) { NovoProjetoNome = "Residência Gama" };

        await viewModel.CriarProjetoCommand.ExecuteAsync(null);

        viewModel.Projetos.Should().ContainSingle(p => p.Nome == "Residência Gama");
        viewModel.NovoProjetoNome.Should().BeEmpty();
    }

    [Fact]
    public async Task Criar_projeto_sem_nome_nao_deve_chamar_api()
    {
        var apiClient = Substitute.For<IApiClient>();
        var viewModel = new ProjetosViewModel(apiClient) { NovoProjetoNome = "   " };

        await viewModel.CriarProjetoCommand.ExecuteAsync(null);

        await apiClient.DidNotReceive().CriarProjetoAsync(Arg.Any<CriarProjetoRequest>(), Arg.Any<CancellationToken>());
    }
}
