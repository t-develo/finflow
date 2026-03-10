using FinFlow.Domain.Entities;
using FinFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Data;

public class FinFlowDbContext : IdentityDbContext<ApplicationUser>
{
    public FinFlowDbContext(DbContextOptions<FinFlowDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ClassificationRule> ClassificationRules => Set<ClassificationRule>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Category: FKはUserId(string)のみで管理。ナビゲーションはInfrastructure側で設定
        builder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Color).HasMaxLength(7).HasDefaultValue("#6B7280");
            e.Property(c => c.UserId).HasMaxLength(450);
            e.HasIndex(c => c.UserId).HasDatabaseName("IX_Categories_UserId");
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);
        });

        builder.Entity<Expense>(e =>
        {
            e.HasKey(ex => ex.Id);
            e.Property(ex => ex.UserId).HasMaxLength(450).IsRequired();
            e.Property(ex => ex.Amount).HasColumnType("decimal(18,2)");
            e.Property(ex => ex.Description).HasMaxLength(500);
            e.Property(ex => ex.ImportSource).HasMaxLength(50);
            e.HasIndex(ex => ex.UserId).HasDatabaseName("IX_Expenses_UserId");
            e.HasIndex(ex => ex.Date).HasDatabaseName("IX_Expenses_Date");
            e.HasIndex(ex => ex.CategoryId).HasDatabaseName("IX_Expenses_CategoryId");
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(ex => ex.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ex => ex.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(ex => ex.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        builder.Entity<Subscription>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.UserId).HasMaxLength(450).IsRequired();
            e.Property(s => s.ServiceName).HasMaxLength(200).IsRequired();
            e.Property(s => s.Amount).HasColumnType("decimal(18,2)");
            e.Property(s => s.BillingCycle).HasMaxLength(20).HasDefaultValue("monthly");
            e.Property(s => s.Notes).HasMaxLength(500);
            e.HasIndex(s => s.UserId).HasDatabaseName("IX_Subscriptions_UserId");
            e.HasIndex(s => s.NextBillingDate).HasDatabaseName("IX_Subscriptions_NextBillingDate");
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Category)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        builder.Entity<ClassificationRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.UserId).HasMaxLength(450).IsRequired();
            e.Property(r => r.Keyword).HasMaxLength(200).IsRequired();
            e.HasIndex(r => r.UserId).HasDatabaseName("IX_ClassificationRules_UserId");
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Category)
                .WithMany(c => c.ClassificationRules)
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed system categories
        var now = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc);
        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "食費", Color = "#F59E0B", IsSystem = true, CreatedAt = now, UpdatedAt = now },
            new Category { Id = 2, Name = "交通費", Color = "#3B82F6", IsSystem = true, CreatedAt = now, UpdatedAt = now },
            new Category { Id = 3, Name = "娯楽", Color = "#8B5CF6", IsSystem = true, CreatedAt = now, UpdatedAt = now },
            new Category { Id = 4, Name = "日用品", Color = "#10B981", IsSystem = true, CreatedAt = now, UpdatedAt = now },
            new Category { Id = 5, Name = "医療費", Color = "#EF4444", IsSystem = true, CreatedAt = now, UpdatedAt = now },
            new Category { Id = 6, Name = "光熱費", Color = "#F97316", IsSystem = true, CreatedAt = now, UpdatedAt = now },
            new Category { Id = 7, Name = "通信費", Color = "#06B6D4", IsSystem = true, CreatedAt = now, UpdatedAt = now },
            new Category { Id = 8, Name = "その他", Color = "#6B7280", IsSystem = true, CreatedAt = now, UpdatedAt = now }
        );
    }
}
