namespace BellucSketch.Domain.Common;

/// <summary>
/// Base para todas as entidades do domínio. Identidade (Id), não os atributos, define igualdade.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity() => Id = Guid.NewGuid();

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
            throw new DomainException("Identificador de entidade não pode ser vazio.");

        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
