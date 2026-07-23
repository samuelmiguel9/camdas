using BellucSketch.Mobile.Services;
using BellucSketch.Mobile.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BellucSketch.Mobile.Tests.ViewModels;

public class LoginViewModelTests
{
    [Fact]
    public async Task Entrar_com_id_valido_deve_salvar_token_e_disparar_evento()
    {
        var apiClient = Substitute.For<IApiClient>();
        var tokenStore = Substitute.For<ITokenStore>();
        var usuarioId = Guid.NewGuid();
        apiClient.LoginDevAsync(usuarioId, Arg.Any<CancellationToken>()).Returns("token-fake");

        var viewModel = new LoginViewModel(apiClient, tokenStore) { UsuarioId = usuarioId.ToString() };
        var eventoDisparado = false;
        viewModel.LoginRealizado += (_, _) => eventoDisparado = true;

        await viewModel.EntrarCommand.ExecuteAsync(null);

        eventoDisparado.Should().BeTrue();
        viewModel.MensagemErro.Should().BeNull();
        await tokenStore.Received(1).SalvarTokenAsync("token-fake");
    }

    [Fact]
    public async Task Entrar_com_id_invalido_nao_deve_chamar_api()
    {
        var apiClient = Substitute.For<IApiClient>();
        var tokenStore = Substitute.For<ITokenStore>();

        var viewModel = new LoginViewModel(apiClient, tokenStore) { UsuarioId = "não-é-um-guid" };

        await viewModel.EntrarCommand.ExecuteAsync(null);

        viewModel.MensagemErro.Should().NotBeNullOrEmpty();
        await apiClient.DidNotReceive().LoginDevAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falha_na_api_deve_preencher_mensagem_de_erro()
    {
        var apiClient = Substitute.For<IApiClient>();
        var tokenStore = Substitute.For<ITokenStore>();
        apiClient.LoginDevAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new HttpRequestException("indisponível")));

        var viewModel = new LoginViewModel(apiClient, tokenStore) { UsuarioId = Guid.NewGuid().ToString() };

        await viewModel.EntrarCommand.ExecuteAsync(null);

        viewModel.MensagemErro.Should().Contain("indisponível");
        viewModel.EstaCarregando.Should().BeFalse();
    }
}
