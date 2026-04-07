namespace WebTinTuc.Models;

public class HomeIndexViewModel
{
    public bool IsDatabaseConnected { get; set; }

    public string DatabasePath { get; set; } = string.Empty;

    public int TableCount { get; set; }

    public List<string> TableNames { get; set; } = new();

    public string? ErrorMessage { get; set; }
}
