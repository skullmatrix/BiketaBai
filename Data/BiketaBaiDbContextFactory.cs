using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BiketaBai.Data;

public class BiketaBaiDbContextFactory : IDesignTimeDbContextFactory<BiketaBaiDbContext>
{
    public BiketaBaiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BiketaBaiDbContext>();
        
        var connectionString = "Server=localhost;Port=3306;Database=biketabai;User=root;Password=;";
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        
        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new BiketaBaiDbContext(optionsBuilder.Options);
    }
}

