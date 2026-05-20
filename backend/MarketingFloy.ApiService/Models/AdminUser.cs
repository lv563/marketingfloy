namespace MarketingFloy.ApiService.Models;

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
