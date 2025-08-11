using Bot.Models;
using Microsoft.EntityFrameworkCore;

namespace Bot.Data;

public class SoundboardDbContext : DbContext
{
    public DbSet<Sound> Sounds { get; set; }

    public SoundboardDbContext(DbContextOptions<SoundboardDbContext> options) : base(options)
    {
    }
}