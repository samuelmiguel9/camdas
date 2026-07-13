using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using MediatR;

namespace Camdas.Application.Plantas;

public sealed record ObterArquivoPlantaQuery(Guid PlantaId) : IRequest<Stream>;

public sealed class ObterArquivoPlantaQueryHandler(
    IPlantaRepository plantaRepository,
    IArquivoStorage arquivoStorage) : IRequestHandler<ObterArquivoPlantaQuery, Stream>
{
    public async Task<Stream> Handle(ObterArquivoPlantaQuery request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        return await arquivoStorage.AbrirAsync(planta.CaminhoArquivoOriginal, cancellationToken);
    }
}
