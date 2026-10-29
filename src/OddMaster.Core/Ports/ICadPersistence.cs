using System;
using System.Collections.Generic;

namespace OddMaster.Core.Ports
{
    /// <summary>Результат загрузки документа: объекты, предупреждения, режим «только чтение».</summary>
    public sealed class CadLoadResult
    {
        /// <summary>Создаёт результат.</summary>
        public CadLoadResult(IReadOnlyList<CadObjectSnapshot> objects, IReadOnlyList<string> warnings,
            bool isReadOnly)
        {
            Objects = objects ?? throw new ArgumentNullException(nameof(objects));
            Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
            IsReadOnly = isReadOnly;
        }

        /// <summary>Объекты в порядке дескрипторов.</summary>
        public IReadOnlyList<CadObjectSnapshot> Objects { get; }

        /// <summary>Предупреждения: миграция, неизвестная версия, отсутствующий модуль.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Документ открыт только для чтения.</summary>
        public bool IsReadOnly { get; }
    }

    /// <summary>
    /// Хранение данных объектов (§1.1). Формат версионирован: старая версия читается миграцией,
    /// неизвестная будущая — только чтение с предупреждением (правило 6).
    /// </summary>
    public interface ICadPersistence
    {
        /// <summary>Сохраняет документ в строку.</summary>
        string Save();

        /// <summary>Загружает документ из строки. moduleAvailable = false — режим «модуль не загружен».</summary>
        CadLoadResult Load(string json, bool moduleAvailable = true);

        /// <summary>Сохраняет в файл (кодировка UTF-8 без BOM, перевод строки LF).</summary>
        void SaveToFile(string path);

        /// <summary>Загружает из файла; имена файлов ищутся без учёта регистра, CRLF терпится (правило 8).</summary>
        CadLoadResult LoadFromFile(string path, bool moduleAvailable = true);
    }
}
