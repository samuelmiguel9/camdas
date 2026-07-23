using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record ObterImagemCamadaQuery(Guid PlantaId, Guid CamadaId) : IRequest<Stream>;

public sealed class ObterImagemCamadaQueryHandler(
    IPlantaRepository plantaRepository,
    IArquivoStorage arquivoStorage) : IRequestHandler<ObterImagemCamadaQuery, Stream>
{
    public async Task<Stream> Handle(ObterImagemCamadaQuery request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        var camada = planta.Camadas.FirstOrDefault(c => c.Id == request.CamadaId)
            ?? throw new RecursoNaoEncontradoException($"Camada '{request.CamadaId}' não encontrada.");

        if (camada.ImagemRasterCaminho is null)
            throw new RecursoNaoEncontradoException($"Camada '{request.CamadaId}' ainda não possui imagem.");

        return await arquivoStorage.AbrirAsync(camada.ImagemRasterCaminho, cancellationToken);
    }
}
