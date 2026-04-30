using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Knowledge;
using Bitcraft.Infrastructure.Persistence;
using Bitcraft.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Bitcraft.InfrastructureIntegrationTests.Persistence;

internal sealed class MutableCurrentUserContext(Guid? userId = null, params string[] roles) : ICurrentUserContext
{
    private readonly HashSet<string> roles = new(roles, StringComparer.OrdinalIgnoreCase);

    public bool IsAuthenticated => UserIdOrNull.HasValue;
    public Guid UserId => UserIdOrNull ?? throw new InvalidOperationException("No current user is set for this test context.");
    public Guid? UserIdOrNull { get; set; } = userId;

    public bool IsInRole(string role) => roles.Contains(role);
}

internal static class PersistenceTestContextFactory
{
    public static AppDbContext Create(MutableCurrentUserContext currentUser)
    {
        var interceptor = new EntitySaveChangesInterceptor(currentUser);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bitcraft-integration-{Guid.NewGuid()}")
            .AddInterceptors(interceptor)
            .Options;

        return new TestAppDbContext(options);
    }

    public static async Task AddUserAsync(AppDbContext db, Guid userId)
    {
        db.Users.Add(new AppUser
        {
            Id = userId,
            Email = $"user-{userId:N}@bitcraft.test",
            UserName = $"user-{userId:N}@bitcraft.test"
        });

        await db.SaveChangesAsync();
    }
}

internal sealed class TestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Ignore<KnowledgeChunk>();
    }
}
