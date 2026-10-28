using System.Globalization;
using System.Text;

namespace OddMaster.Render.Dxf
{
    /// <summary>
    /// Кодирование текста для DXF R12: файл остаётся ASCII, а кириллица пишется
    /// escape-последовательностями \U+XXXX, которые понимают AutoCAD, nanoCAD и ezdxf.
    /// </summary>
    public static class DxfTextCodec
    {
        /// <summary>Кодирует строку: ASCII как есть, остальные символы — \U+XXXX (UTF-16 code unit).</summary>
        public static string Encode(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text!.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '\r':
                    case '\n':
                    case '\t':
                        sb.Append(' ');
                        break;
                    default:
                        if (c < 0x80) sb.Append(c);
                        else sb.Append("\\U+").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Обратное преобразование (для тестов и отчётов).</summary>
        public static string Decode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return string.Empty;
            var sb = new StringBuilder(encoded!.Length);
            for (var i = 0; i < encoded.Length; i++)
            {
                if (encoded[i] == '\\' && i + 6 < encoded.Length && encoded[i + 1] == 'U' && encoded[i + 2] == '+')
                {
                    var hex = encoded.Substring(i + 3, 4);
                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                    {
                        sb.Append((char)code);
                        i += 6;
                        continue;
                    }
                }
                sb.Append(encoded[i]);
            }
            return sb.ToString();
        }
    }
}
