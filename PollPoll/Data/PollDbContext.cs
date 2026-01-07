using Microsoft.EntityFrameworkCore;
using PollPoll.Models;

namespace PollPoll.Data;

/// <summary>
/// Entity Framework Core database context for PollPoll application.
/// </summary>
public class PollDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PollDbContext"/> class.
    /// </summary>
    /// <param name="options">Database context options.</param>
    public PollDbContext(DbContextOptions<PollDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the Polls DbSet.
    /// </summary>
    public DbSet<Poll> Polls { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Options DbSet.
    /// </summary>
    public DbSet<Option> Options { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Votes DbSet.
    /// </summary>
    public DbSet<Vote> Votes { get; set; } = null!;

    /// <summary>
    /// Configures the model relationships and constraints.
    /// </summary>
    /// <param name="modelBuilder">Model builder instance.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Poll entity
        modelBuilder.Entity<Poll>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Code)
                .IsRequired()
                .HasMaxLength(4);

            entity.Property(p => p.Question)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(p => p.ChoiceMode)
                .IsRequired();

            entity.Property(p => p.IsClosed)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(p => p.CreatedAt)
                .IsRequired();

            // Unique index on Code for fast lookups
            entity.HasIndex(p => p.Code)
                .IsUnique();

            // Index on IsClosed for dashboard filtering
            entity.HasIndex(p => p.IsClosed);

            // Configure relationships
            entity.HasMany(p => p.Options)
                .WithOne(o => o.Poll)
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.Votes)
                .WithOne(v => v.Poll)
                .HasForeignKey(v => v.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Option entity
        modelBuilder.Entity<Option>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.Text)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(o => o.DisplayOrder)
                .IsRequired();

            // Index on PollId for querying options for a poll
            entity.HasIndex(o => o.PollId);

            // Configure relationships
            entity.HasMany(o => o.Votes)
                .WithOne(v => v.Option)
                .HasForeignKey(v => v.OptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Vote entity
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(v => v.Id);

            entity.Property(v => v.VoterId)
                .IsRequired();

            entity.Property(v => v.SubmittedAt)
                .IsRequired();

            // Composite index on (PollId, VoterId) for duplicate detection
            entity.HasIndex(v => new { v.PollId, v.VoterId });

            // Index on OptionId for aggregation queries
            entity.HasIndex(v => v.OptionId);
        });
    }
}
