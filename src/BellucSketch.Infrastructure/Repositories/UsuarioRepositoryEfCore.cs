using BellucSketch.Application.Abstractions;
using BellucSketch.Domain.Entities;
using BellucSketch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BellucSketch.Infrastructure.Repositories;

public sealed class UsuarioRepositoryEfCore(BellucSketchDbContext dbContext) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Usuarios.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> ExisteComEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Usuarios.AnyAsync(u => u.Email == email, cancellationToken);

    public void Adicionar(Usuario usuario) => dbContext.Usuarios.Add(usuario);
}
