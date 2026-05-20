namespace MarketingFloy.ApiService.Models;

public class ContentItem
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string Type { get; set; } = "text"; // text | html | image
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
