using Microsoft.EntityFrameworkCore;
using NewComputerStore.Models;

namespace NewComputerStore.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<ProductColor> ProductColors { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }

        // --- Dynamic Spec System ---
        public DbSet<SpecGroup> SpecGroups { get; set; }
        public DbSet<SpecKey> SpecKeys { get; set; }
        public DbSet<ProductSpec> ProductSpecs { get; set; }
        public DbSet<ChatConversation> ChatConversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<QuickReply> QuickReplies { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships if needed, though attributes handle most of them
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Product)
                .WithMany(p => p.OrderDetails)
                .HasForeignKey(od => od.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Dynamic Spec System ---
            // SpecGroup → SpecKey (cascade)
            modelBuilder.Entity<SpecKey>()
                .HasOne(sk => sk.Group)
                .WithMany(sg => sg.SpecKeys)
                .HasForeignKey(sk => sk.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // SpecKey → ProductSpec (Restrict to avoid multiple cascade path cycle)
            modelBuilder.Entity<ProductSpec>()
                .HasOne(ps => ps.SpecKey)
                .WithMany(sk => sk.ProductSpecs)
                .HasForeignKey(ps => ps.SpecKeyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Product → ProductSpec (cascade)
            modelBuilder.Entity<ProductSpec>()
                .HasOne(ps => ps.Product)
                .WithMany(p => p.ProductSpecs)
                .HasForeignKey(ps => ps.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique: one value per product per spec key
            modelBuilder.Entity<ProductSpec>()
                .HasIndex(ps => new { ps.ProductId, ps.SpecKeyId })
                .IsUnique();

            // --- Chat System ---
            modelBuilder.Entity<ChatConversation>()
                .HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatConversation>()
                .HasOne(c => c.Staff)
                .WithMany()
                .HasForeignKey(c => c.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Indexing for Discount System ---
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.DiscountPercent);
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.IsDeleted);
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.IsHidden);
            modelBuilder.Entity<OrderDetail>()
                .HasIndex(od => od.FinalPrice);
        }
    }
}
