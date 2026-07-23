namespace BellucSketch.Mobile.Exportacao;

/// <summary>
/// Formato de arquivo portátil de uma planta (JSON, ver <see cref="ArquivoPlantaService"/>): reúne a
/// imagem base + o traço de cada camada (ordem, opacidade, visibilidade, bloqueios) num único
/// arquivo, pra enviar a outro dispositivo (WhatsApp, Drive, e-mail, Bluetooth — qualquer app que
/// aceite compartilhar um arquivo) e reabrir lá como uma planta nova, sem depender do mesmo projeto
/// nem de rede entre os dois aparelhos no momento da transferência.
/// </summary>
public sealed record ArquivoPlantaDto(
    string FormatoVersao,
    string Nome,
    string? Descricao,
    string? NomeCliente,
    byte[] ImagemBasePng,
    IReadOnlyList<ArquivoCamadaDto> Camadas);

public sealed record ArquivoCamadaDto(
    string Nome,
    bool Visivel,
    bool Bloqueada,
    bool BloqueioAlpha,
    int Ordem,
    double Opacidade,
    byte[]? ImagemPng);
