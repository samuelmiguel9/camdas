using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using MediatR;

namespace BellucSketch.Application.Plantas;

public sealed record RemoverPlantaCommand(Guid PlantaId) : IRequest;

public sealed class RemoverPlantaCommandHandler(
    IPlantaRepository plantaRepository,
    IArquivoStorage arquivoStorage,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoverPlantaCommand>
{
    public async Task Handle(RemoverPlantaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        // Guardados ANTES de remover — a planta inteira (e toda referência aos arquivos dela: a
        // imagem base + o raster de cada camada) deixa de existir no banco depois de Remover, então
        // sem isto TODOS esses arquivos ficariam órfãos pra sempre no armazenamento.
        var caminhosParaExcluir = new List<string> { planta.CaminhoArquivoOriginal };
        caminhosParaExcluir.AddRange(planta.Camadas.Select(c => c.ImagemRasterCaminho).OfType<string>());

        plantaRepository.Remover(planta);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        foreach (var caminho in caminhosParaExcluir)
            await arquivoStorage.ExcluirAsync(caminho, cancellationToken);
    }
}
