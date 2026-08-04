namespace LotsofLoot.Models.Web;

public sealed record NavItem
{
    public required string Title { get; set; } = string.Empty;
    public string Href { get; set; } = "#";
    public List<NavItem> Children { get; set; } = [];
    public required BasePage BasePage { get; set; }
}

public enum BasePage
{
    Home,
    Settings,
    None,
}
