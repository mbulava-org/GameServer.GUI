using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameServer.Docker.Data
{
    /// <summary>
    /// Factory for creating DbContext instances at design-time (for migrations)
    /// </summary>
    public class GameServerDbContextFactory : IDesignTimeDbContextFactory<GameServerDbContext>
    {
        public GameServerDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GameServerDbContext>();
            
            // Use a temporary in-memory database for design-time operations
            optionsBuilder.UseSqlite("Data Source=:memory:");
            
            return new GameServerDbContext(optionsBuilder.Options);
        }
    }
}
