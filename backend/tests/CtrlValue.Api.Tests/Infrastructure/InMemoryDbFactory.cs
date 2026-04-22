using Microsoft.EntityFrameworkCore;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Tests.Infrastructure;

/// <summary>
/// Provides a fresh in-memory <see cref="AppDbContext"/> for each unit test.
/// Uses EF Core's InMemory provider for speed and to avoid FK constraint issues
/// in isolated unit tests that don't seed full entity graphs.
/// Each call creates a unique database name so tests are fully isolated from one another.
/// </summary>
public static class InMemoryDbFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UnitTestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
