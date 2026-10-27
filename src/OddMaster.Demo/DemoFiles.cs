using System;
using System.IO;
using System.Text;

namespace OddMaster.Demo
{
    /// <summary>Детерминированная запись артефактов: UTF-8 без BOM, LF, без времени и случайности.</summary>
    public static class DemoFiles
    {
        /// <summary>Записывает текстовый файл в docs/demo (относительный путь от корня репозитория).</summary>
        public static void WriteText(string root, string relativePath, string content)
        {
            var full = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? root);
            var normalized = content.Replace("\r\n", "\n");
            File.WriteAllText(full, normalized, new UTF8Encoding(false));
        }

        /// <summary>Ищет корень репозитория по OddMaster.sln.</summary>
        public static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OddMaster.sln")))
                dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Не найден OddMaster.sln");
            return dir.FullName;
        }
    }
}
