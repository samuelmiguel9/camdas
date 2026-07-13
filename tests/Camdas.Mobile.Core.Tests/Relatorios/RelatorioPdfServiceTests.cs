using System.Text;
using Camdas.Mobile.Relatorios;
using FluentAssertions;
using Xunit;

namespace Camdas.Mobile.Tests.Relatorios;

public class RelatorioPdfServiceTests
{
    [Fact]
    public void Gerar_deve_produzir_um_pdf_valido_e_nao_vazio()
    {
        var bytes = RelatorioPdfService.Gerar();

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Gerar_deve_comecar_pela_versao_1_0()
    {
        HistoricoVersoes.Todas.Should().NotBeEmpty();
        HistoricoVersoes.Todas[0].Versao.Should().Be("1.0");
    }
}
