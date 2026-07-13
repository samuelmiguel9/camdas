using Camdas.Domain.Common;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Camdas.Domain.Tests.Entities;

public class PlantaTests
{
    private static Planta NovaPlanta() => new(
        projetoId: Guid.NewGuid(),
        importadoPorId: Guid.NewGuid(),
        nome: "Casa Alfa",
        descricao: null,
        nomeCliente: null,
        tipoArquivoOrigem: TipoArquivoOrigem.Imagem,
        caminhoArquivoOriginal: "/intranet/plantas/casa-alfa.png",
        dataImportacao: DateTime.UtcNow);

    [Fact]
    public void Importar_planta_nao_deve_criar_nenhuma_camada()
    {
        var planta = NovaPlanta();

        planta.Camadas.Should().BeEmpty();
    }

    [Fact]
    public void Deve_aceitar_descricao_e_nome_do_cliente_opcionais()
    {
        var planta = new Planta(
            Guid.NewGuid(), Guid.NewGuid(), "Casa Beta", "Reforma completa", "João Silva",
            TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow);

        planta.Descricao.Should().Be("Reforma completa");
        planta.NomeCliente.Should().Be("João Silva");
    }

    [Fact]
    public void Nao_deve_importar_planta_sem_nome()
    {
        var criar = () => new Planta(
            Guid.NewGuid(), Guid.NewGuid(), "", null, null, TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow);

        criar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deve_adicionar_camada_visivel_desbloqueada_e_com_a_proxima_ordem_disponivel()
    {
        var planta = NovaPlanta();

        var camada1 = planta.AdicionarCamada("Instalação elétrica");
        var camada2 = planta.AdicionarCamada("Hidráulica");

        camada1.Nome.Should().Be("Instalação elétrica");
        camada1.Visivel.Should().BeTrue();
        camada1.Bloqueada.Should().BeFalse();
        camada1.Ordem.Should().Be(1);
        camada2.Ordem.Should().Be(2);
        planta.Camadas.Select(c => c.Id).Should().Equal(camada1.Id, camada2.Id);
    }

    [Fact]
    public void Deve_reordenar_camadas_e_renumerar_em_ordem_crescente()
    {
        var planta = NovaPlanta();
        var camada1 = planta.AdicionarCamada("Primeira");
        var camada2 = planta.AdicionarCamada("Segunda");
        var camada3 = planta.AdicionarCamada("Terceira");

        planta.ReordenarCamadas([camada3.Id, camada1.Id, camada2.Id]);

        planta.Camadas.Select(c => c.Id).Should().Equal(camada3.Id, camada1.Id, camada2.Id);
        camada3.Ordem.Should().Be(1);
        camada1.Ordem.Should().Be(2);
        camada2.Ordem.Should().Be(3);
    }

    [Fact]
    public void Nao_deve_reordenar_com_lista_que_nao_bate_com_as_camadas_existentes()
    {
        var planta = NovaPlanta();
        var camada1 = planta.AdicionarCamada("Primeira");
        planta.AdicionarCamada("Segunda");

        var reordenar = () => planta.ReordenarCamadas([camada1.Id, Guid.NewGuid()]);

        reordenar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Desbloquear_camada_deve_permitir_editar_novamente()
    {
        var planta = NovaPlanta();
        var camada = planta.AdicionarCamada("Estrutura");
        planta.BloquearCamada(camada.Id);
        planta.DesbloquearCamada(camada.Id);

        var atualizar = () => planta.AtualizarImagemRasterDaCamada(camada.Id, "/intranet/camadas/traco.png");

        atualizar.Should().NotThrow();
    }

    [Fact]
    public void Deve_remover_camada()
    {
        var planta = NovaPlanta();
        var camada = planta.AdicionarCamada("Mobiliário");

        var remover = () => planta.RemoverCamada(camada.Id);

        remover.Should().NotThrow();
        planta.Camadas.Should().NotContain(c => c.Id == camada.Id);
    }

    [Fact]
    public void Deve_atualizar_imagem_raster_de_camada_desbloqueada()
    {
        var planta = NovaPlanta();
        var camada = planta.AdicionarCamada("Hidráulica");

        planta.AtualizarImagemRasterDaCamada(camada.Id, "/intranet/camadas/traco.png");

        camada.ImagemRasterCaminho.Should().Be("/intranet/camadas/traco.png");
    }

    [Fact]
    public void Nao_deve_atualizar_imagem_raster_de_camada_bloqueada()
    {
        var planta = NovaPlanta();
        var camada = planta.AdicionarCamada("Elétrica");
        planta.BloquearCamada(camada.Id);

        var atualizar = () => planta.AtualizarImagemRasterDaCamada(camada.Id, "/intranet/camadas/traco.png");

        atualizar.Should().Throw<DomainException>().WithMessage("*bloqueada*");
    }
}
