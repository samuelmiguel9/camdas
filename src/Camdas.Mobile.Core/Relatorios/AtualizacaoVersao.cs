namespace Camdas.Mobile.Relatorios;

/// <summary>Uma entrada do changelog do app, exibida no relatório em PDF (aba "Relatório").</summary>
public sealed record AtualizacaoVersao(
    string Versao,
    DateTime DataHora,
    string Resumo,
    IReadOnlyList<string> Itens,
    IReadOnlyList<string> BugsCorrigidos);
