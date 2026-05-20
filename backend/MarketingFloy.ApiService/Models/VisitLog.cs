namespace MarketingFloy.ApiService.Models;

public class VisitLog
{
    public int Id { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Pagina { get; set; }
    public DateTime FechaVisita { get; set; } = DateTime.UtcNow;
}
