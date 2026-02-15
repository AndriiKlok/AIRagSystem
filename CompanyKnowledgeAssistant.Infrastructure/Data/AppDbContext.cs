using CompanyKnowledgeAssistant.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompanyKnowledgeAssistant.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Area> Areas { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<Chunk> Chunks { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Areas
        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.Name);
        });

        // Documents
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.FilePath).IsRequired();
            entity.Property(e => e.ProcessingStatus).IsRequired().HasDefaultValue("Uploading");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.AreaId);
            entity.HasIndex(e => e.ProcessingStatus);
            entity.HasOne(d => d.Area).WithMany(a => a.Documents).HasForeignKey(d => d.AreaId).OnDelete(DeleteBehavior.Cascade);
        });

        // Chunks
        modelBuilder.Entity<Chunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Embedding).IsRequired();
            entity.HasIndex(e => e.DocumentId);
            entity.HasOne(c => c.Document).WithMany(d => d.Chunks).HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        // Chats
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.AreaId);
            entity.HasIndex(e => e.LastMessageAt).IsDescending();
            entity.HasOne(c => c.Area).WithMany(a => a.Chats).HasForeignKey(c => c.AreaId).OnDelete(DeleteBehavior.Cascade);
        });

        // Messages
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.ChatId);
            entity.HasIndex(e => e.CreatedAt).IsDescending();
            entity.HasOne(m => m.Chat).WithMany(c => c.Messages).HasForeignKey(m => m.ChatId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}