using App.Domain.Content;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application;

// Implemented by App.Infrastructure.AppDbContext, so use cases never reference Infrastructure.
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserTopic> UserTopics { get; }
    DbSet<Topic> Topics { get; }
    DbSet<Concept> Concepts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
