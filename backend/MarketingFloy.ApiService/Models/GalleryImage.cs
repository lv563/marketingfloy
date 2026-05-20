namespace MarketingFloy.ApiService.Models;

public class GalleryImage
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public string UrlImagen { get; set; } = "";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
}
