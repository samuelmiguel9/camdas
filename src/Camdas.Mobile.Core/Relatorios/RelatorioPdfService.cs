using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Camdas.Mobile.Relatorios;

/// <summary>
/// Gera o relatório de atualizações do app em PDF, um bloco por versão (ordem crescente, 1.0
/// primeiro), com data/hora, o que foi feito e quais bugs foram corrigidos em teste. Lógica pura
/// (QuestPDF/SkiaSharp), sem dependência de MAUI — testável num host xUnit comum.
/// </summary>
public static class RelatorioPdfService
{
    static RelatorioPdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Gerar(IReadOnlyList<AtualizacaoVersao>? versoes = null)
    {
        versoes ??= HistoricoVersoes.Todas;

        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(36);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                pagina.Header().Column(cabecalho =>
                {
                    cabecalho.Item().Text("Camdas — Relatório de Atualizações").FontSize(20).Bold();
                    cabecalho.Item().Text("developed by Samuel Miguel").FontSize(10).FontColor(Colors.Grey.Darken1);
                    cabecalho.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                pagina.Content().PaddingTop(12).Column(coluna =>
                {
                    foreach (var versao in versoes)
                    {
                        coluna.Item().PaddingBottom(16).Column(bloco =>
                        {
                            bloco.Item().Text($"Versão {versao.Versao}").FontSize(15).Bold();
                            bloco.Item().Text(versao.DataHora.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken1);
                            bloco.Item().PaddingTop(4).Text(versao.Resumo).Italic();

                            if (versao.Itens.Count > 0)
                            {
                                bloco.Item().PaddingTop(6).Text("O que foi feito:").Bold();
                                foreach (var item in versao.Itens)
                                    bloco.Item().PaddingLeft(12).Text($"• {item}");
                            }

                            if (versao.BugsCorrigidos.Count > 0)
                            {
                                bloco.Item().PaddingTop(6).Text("Bugs corrigidos em teste:").Bold();
                                foreach (var bug in versao.BugsCorrigidos)
                                    bloco.Item().PaddingLeft(12).Text($"• {bug}");
                            }
                        });
                    }
                });

                pagina.Footer().AlignCenter().Text(texto =>
                {
                    texto.CurrentPageNumber();
                    texto.Span(" / ");
                    texto.TotalPages();
                });
            });
        });

        return documento.GeneratePdf();
    }
}
