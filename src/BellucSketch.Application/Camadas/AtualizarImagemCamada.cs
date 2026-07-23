using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record AtualizarImagemCamadaCommand(
    Guid PlantaId,
    Guid CamadaId,
    string NomeArquivo,
    Stream Conteudo) : IRequest<CamadaDto>;

public sealed class AtualizarImagemCamadaCommandValidator : AbstractValidator<AtualizarImagemCamadaCommand>
{
    public AtualizarImagemCamadaCommandValidator()
    {
        RuleFor(c => c.PlantaId).NotEmpty();
        RuleFor(c => c.CamadaId).NotEmpty();
        RuleFor(c => c.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(c => c.Conteudo).NotNull();
    }
}

public sealed class AtualizarImagemCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IArquivoStorage arquivoStorage,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<AtualizarImagemCamadaCommand, CamadaDto>
{
    public async Task<CamadaDto> Handle(AtualizarImagemCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        // Guardado ANTES de trocar — sem isto, o PNG anterior da camada ficava órfão no
        // armazenamento pra sempre a cada "Salvar" (bug real: espaço de Storage crescendo sem
        // parar, um arquivo novo por save, nenhum sendo apagado).
        var caminhoAnterior = planta.Camadas.First(c => c.Id == request.CamadaId).ImagemRasterCaminho;

        var caminho = await arquivoStorage.SalvarAsync(request.NomeArquivo, request.Conteudo, cancellationToken);

        planta.AtualizarImagemRasterDaCamada(request.CamadaId, caminho);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            request.CamadaId,
            TipoAcaoHistorico.CamadaImagemAtualizada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        // Só depois de confirmar a troca no banco — apagar antes e o SalvarAlteracoesAsync falhar
        // deixaria a camada apontando pra um arquivo que não existe mais.
        if (caminhoAnterior is not null)
            await arquivoStorage.ExcluirAsync(caminhoAnterior, cancellationToken);

        return planta.Camadas.First(c => c.Id == request.CamadaId).ParaDto();
    }
}
