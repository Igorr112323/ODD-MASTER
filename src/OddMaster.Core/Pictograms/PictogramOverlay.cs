using System;
using System.Text.RegularExpressions;

namespace OddMaster.Core.Pictograms
{
    /// <summary>Накладывает сгенерированный знак поверх растра страницы прил. Б (проверка оцифровки).</summary>
    public static class PictogramOverlay
    {
        /// <summary>Возвращает SVG: растр PNG внизу, знак сверху полупрозрачно.</summary>
        public static string Overlay(string pngBase64, string signSvg)
        {
            if (pngBase64 == null) throw new ArgumentNullException(nameof(pngBase64));
            if (signSvg == null) throw new ArgumentNullException(nameof(signSvg));
            var match = Regex.Match(signSvg, "viewBox\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            var viewBox = match.Success ? match.Groups[1].Value : "0 0 800 800";
            var parts = viewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var width = parts.Length > 2 ? parts[2] : "800";
            var height = parts.Length > 3 ? parts[3] : "800";
            var inner = signSvg.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "", StringComparison.Ordinal);
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                + "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\""
                + viewBox + "\" width=\"" + width + "\" height=\"" + height + "\">\n"
                + "  <image x=\"0\" y=\"0\" width=\"" + width + "\" height=\"" + height
                + "\" xlink:href=\"data:image/png;base64," + pngBase64 + "\"/>\n"
                + "  <g opacity=\"0.55\">" + inner + "</g>\n"
                + "</svg>\n";
        }
    }
}
