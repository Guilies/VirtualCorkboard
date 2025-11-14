using System.Windows.Media;

namespace VirtualCorkboard.Twine
{
 public class TwineStyle
 {
 public Color TwineColor { get; set; } = Colors.Red;
 public TwineTextureType Texture { get; set; } = TwineTextureType.Solid;
 public double Thickness { get; set; } =5.0;
 public Color HighlightColor { get; set; } = Colors.CornflowerBlue;
 }
}
