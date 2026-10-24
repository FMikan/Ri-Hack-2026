using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.Application;

// Implemented by App.Infrastructure.AppDbContext, so use cases never reference Infrastructure.
public interface IAppDbContext
{
    DbSet<Note> Notes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
