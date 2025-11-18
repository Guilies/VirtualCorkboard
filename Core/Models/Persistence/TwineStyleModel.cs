using System.Windows.Media;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Persistence.Models
{
    public class TwineStyleModel
    {
        public string Color { get; set; } = "#FF0000"; // hex
        public string HighlightColor { get; set; } = "#6495ED"; // CornflowerBlue
        public TwineTextureType Texture { get; set; } = TwineTextureType.Solid;
        public double Thickness { get; set; } =5.0;
    }
}
