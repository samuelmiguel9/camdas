using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record DuplicarCamadaCommand(Guid PlantaId, Guid CamadaId) : IRequest<CamadaDto>;

public sealed class DuplicarCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IArquivoStorage arquivoStorage,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<DuplicarCamadaCommand, CamadaDto>
{
    public async Task<CamadaDto> Handle(DuplicarCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        var original = planta.Camadas.First(c => c.Id == request.CamadaId);
        var caminhoImagemOriginal = original.ImagemRasterCaminho;

        var copia = planta.DuplicarCamada(request.CamadaId);

        if (caminhoImagemOriginal is not null)
        {
            await using var conteudoOriginal = await arquivoStorage.AbrirAsync(caminhoImagemOriginal, cancellationToken);
            var novoCaminho = await arquivoStorage.SalvarAsync($"{copia.Id}.png", conteudoOriginal, cancellationToken);
            planta.AtualizarImagemRasterDaCamada(copia.Id, novoCaminho);
        }

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            copia.Id,
            TipoAcaoHistorico.CamadaDuplicada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return planta.Camadas.First(c => c.Id == copia.Id).ParaDto();
    }
}
