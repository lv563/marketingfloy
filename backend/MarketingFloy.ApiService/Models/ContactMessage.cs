namespace MarketingFloy.ApiService.Models;

public class ContactMessage
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public bool Leido { get; set; }
}
