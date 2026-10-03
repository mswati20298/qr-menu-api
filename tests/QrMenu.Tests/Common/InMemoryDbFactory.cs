using Microsoft.EntityFrameworkCore;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Tests.Common;

public static class InMemoryDbFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
