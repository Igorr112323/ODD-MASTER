using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OddMaster.Core.Config
{
    /// <summary>Загрузка JSON с понятными русскими ошибками (файл, причина, как исправить).</summary>
    public static class JsonConfig
    {
        /// <summary>Читает и разбирает JSON-файл. Битый файл даёт ConfigException, а не «сырое» исключение.</summary>
        public static JsonDocument Load(string root, string relativePath)
        {
            var full = Path.Combine(root, relativePath);
            if (!File.Exists(full))
                throw new ConfigException(relativePath, "", "файл не найден", "создайте файл по образцу из config/schema/");
            string text;
            try { text = File.ReadAllText(full, Encoding.UTF8); }
            catch (IOException ex)
            {
                throw new ConfigException(relativePath, "", "не удалось прочитать файл: " + ex.Message,
                    "проверьте права доступа и кодировку UTF-8");
            }
            try
            {
                return JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException ex)
            {
                throw new ConfigException(relativePath, "", "некорректный JSON: " + ex.Message,
                    "проверьте запятые, кавычки и скобки; сверьтесь с JSON Schema");
            }
        }

        /// <summary>Читает текст UTF-8, если файл есть; иначе возвращает пустую строку.</summary>
        public static string ReadUtf8IfExists(string root, string relativePath)
        {
            var full = Path.Combine(root, relativePath);
            return File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : string.Empty;
        }

        /// <summary>Читает текст UTF-8 без BOM (для эталонных файлов и демо-артефактов).</summary>
        public static string ReadUtf8(string root, string relativePath)
        {
            var full = Path.Combine(root, relativePath);
            if (!File.Exists(full))
                throw new ConfigException(relativePath, "", "файл не найден", "создайте файл");
            return File.ReadAllText(full, Encoding.UTF8);
        }
    }
}
