using Camdas.Domain.Common;

namespace Camdas.Domain.Entities;

/// <summary>
/// Entidade filha do agregado Planta. Só pode ser criada/removida através de Planta, que é o
/// único ponto de entrada externo (Application/Infra/Api/Mobile não instanciam Camada diretamente).
/// Totalmente definida pelo usuário — nome livre, sem categoria fixa. Não tem cor própria: cor é
/// só do traço/linha desenhada (escolhida na hora de desenhar), não da camada em si.
/// </summary>
public sealed class Camada : Entity
{
    public Guid PlantaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public bool Visivel { get; private set; }
    public bool Bloqueada { get; private set; }
    public bool BloqueioAlpha { get; private set; }
    public int Ordem { get; private set; }
    public double Opacidade { get; private set; }
    public string? ImagemRasterCaminho { get; private set; }

    private Camada()
    {
    } // EF Core

    internal Camada(Guid plantaId, string nome, int ordem)
    {
        if (plantaId == Guid.Empty)
            throw new DomainException("Camada precisa estar vinculada a uma planta.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome da camada é obrigatório.");

        PlantaId = plantaId;
        Nome = nome;
        Ordem = ordem;
        Visivel = true;
        Bloqueada = false;
        Opacidade = 1.0;
    }

    internal void AlternarVisibilidade() => Visivel = !Visivel;

    internal void DefinirOpacidade(double opacidade)
    {
        if (opacidade is < 0.0 or > 1.0)
            throw new DomainException("Opacidade precisa estar entre 0 e 1.");

        Opacidade = opacidade;
    }

    internal void Bloquear() => Bloqueada = true;

    internal void Desbloquear() => Bloqueada = false;

    internal void BloquearAlpha() => BloqueioAlpha = true;

    internal void DesbloquearAlpha() => BloqueioAlpha = false;

    internal void DefinirOrdem(int ordem) => Ordem = ordem;

    internal void AtualizarImagemRaster(string caminho)
    {
        GarantirDesbloqueada();

        if (string.IsNullOrWhiteSpace(caminho))
            throw new DomainException("Caminho da imagem raster é obrigatório.");

        ImagemRasterCaminho = caminho;
    }

    /// <summary>Esvazia o traço da camada sem excluí-la — a camada permanece na lista, só perde a imagem raster.</summary>
    internal void Limpar()
    {
        GarantirDesbloqueada();
        ImagemRasterCaminho = null;
    }

    /// <summary>
    /// Cria uma cópia com nome/opacidade/visibilidade iguais aos desta camada, sempre desbloqueada
    /// (senão a cópia nasceria travada para edição) e sem imagem — o caller (Application) é quem
    /// duplica o arquivo raster, já que Domain não conhece IArquivoStorage.
    /// </summary>
    internal Camada Duplicar(int ordem)
    {
        var copia = new Camada(PlantaId, $"{Nome} (cópia)", ordem);
        copia.DefinirOpacidade(Opacidade);
        if (!Visivel)
            copia.AlternarVisibilidade();

        return copia;
    }

    private void GarantirDesbloqueada()
    {
        if (Bloqueada)
            throw new DomainException($"A camada '{Nome}' está bloqueada e não pode ser editada.");
    }
}
