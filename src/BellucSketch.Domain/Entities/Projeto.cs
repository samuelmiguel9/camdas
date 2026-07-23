using BellucSketch.Domain.Common;
using BellucSketch.Domain.Enums;

namespace BellucSketch.Domain.Entities;

public sealed class Projeto : Entity
{
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public Guid CriadoPorId { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public StatusProjeto Status { get; private set; }

    private Projeto()
    {
    } // EF Core

    public Projeto(string nome, string? descricao, Guid criadoPorId, DateTime dataCriacao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome do projeto é obrigatório.");
        if (criadoPorId == Guid.Empty)
            throw new DomainException("Projeto precisa de um responsável pela criação.");

        Nome = nome;
        Descricao = descricao;
        CriadoPorId = criadoPorId;
        DataCriacao = dataCriacao;
        Status = StatusProjeto.Ativo;
    }

    public void Renomear(string novoNome)
    {
        if (string.IsNullOrWhiteSpace(novoNome))
            throw new DomainException("Nome do projeto é obrigatório.");

        Nome = novoNome;
    }

    public void AtualizarDescricao(string? novaDescricao) => Descricao = novaDescricao;

    public void Arquivar()
    {
        if (Status == StatusProjeto.Arquivado)
            throw new DomainException("Projeto já está arquivado.");

        Status = StatusProjeto.Arquivado;
    }

    public void Reativar()
    {
        if (Status == StatusProjeto.Ativo)
            throw new DomainException("Projeto já está ativo.");

        Status = StatusProjeto.Ativo;
    }
}
