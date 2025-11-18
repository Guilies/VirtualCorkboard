namespace VirtualCorkboard.Persistence.Models
{
    public class TextFormatModel
    {
        public double FontSize { get; set; } =16;
        public string? FontFamily { get; set; }
        public string? Weight { get; set; }
        public string? Foreground { get; set; }
        public string? Background { get; set; }
        public string? Alignment { get; set; }
        public double? Padding { get; set; }
    }
}
