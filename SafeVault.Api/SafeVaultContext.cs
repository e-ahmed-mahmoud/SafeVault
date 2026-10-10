using Microsoft.EntityFrameworkCore;

public class SafeVaultContext : DbContext
{
    public SafeVaultContext(DbContextOptions<SafeVaultContext> options) : base(options) { }
    public DbSet<VaultItem> VaultItems => Set<VaultItem>();
}
