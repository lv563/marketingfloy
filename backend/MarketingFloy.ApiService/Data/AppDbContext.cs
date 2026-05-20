using MarketingFloy.ApiService.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketingFloy.ApiService.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<VisitLog> VisitLogs => Set<VisitLog>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
}
