using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace WordMemo
{
    /// <summary>
    /// 词库持久化：XML 保存到 %LocalAppData%\WordMemo\words.xml（4.6.2 无 System.Text.Json，
    /// 为零 NuGet 依赖选用 XmlSerializer）；CSV 导入导出用 UTF-8 BOM，Excel 可直接打开。
    /// </summary>
    public static class WordStore
    {
        private static readonly string DataFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WordMemo");
        private static readonly string DataFilePath = Path.Combine(DataFolderPath, "words.xml");

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(List<WordEntry>));

        public static string DataFile
        {
            get { return DataFilePath; }
        }

        public static List<WordEntry> Load()
        {
            if (!File.Exists(DataFilePath))
            {
                return new List<WordEntry>();
            }

            try
            {
                using (var stream = File.OpenRead(DataFilePath))
                {
                    var list = Serializer.Deserialize(stream) as List<WordEntry>;
                    return list ?? new List<WordEntry>();
                }
            }
            catch
            {
                // 数据文件损坏时不让应用崩溃，返回空词库（原文件保留以便手工恢复）
                return new List<WordEntry>();
            }
        }

        public static void Save(IEnumerable<WordEntry> words)
        {
            Directory.CreateDirectory(DataFolderPath);
            // XmlSerializer 按声明的 List<WordEntry> 根类型工作，直接传 ObservableCollection 会抛异常；
            // 先序列化到内存，再经临时文件原子替换，避免中途失败截断原数据
            var list = new List<WordEntry>(words);
            var serializer = new XmlSerializer(typeof(List<WordEntry>));
            var temporaryPath = DataFilePath + ".tmp";

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, list);
                File.WriteAllBytes(temporaryPath, stream.ToArray());
            }

            if (File.Exists(DataFilePath))
            {
                File.Delete(DataFilePath);
            }
            File.Move(temporaryPath, DataFilePath);
        }

        /// <summary>导出 CSV。字段内的换行替换为"；"，导入侧按行解析，避免多行字段解析复杂化。</summary>
        public static void ExportCsv(string filePath, IEnumerable<WordEntry> words)
        {
            var builder = new StringBuilder();
            builder.AppendLine("单词,音标,释义,例句,分类,备注,熟练度,添加时间");
            foreach (var w in words)
            {
                builder.AppendLine(string.Join(",",
                    EscapeCsv(ReplaceNewline(w.Word)),
                    EscapeCsv(ReplaceNewline(w.Phonetics)),
                    EscapeCsv(ReplaceNewline(w.Meaning)),
                    EscapeCsv(ReplaceNewline(w.Example)),
                    EscapeCsv(ReplaceNewline(w.Category)),
                    EscapeCsv(ReplaceNewline(w.Note)),
                    w.Proficiency.ToString(),
                    w.AddedDate.ToString("yyyy-MM-dd HH:mm:ss")));
            }

            // UTF-8 BOM 让 Excel 正确识别中文
            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(true));
        }

        /// <summary>导入 CSV 并按单词去重合并：已存在的更新空字段，否则新增。返回 (新增数, 更新数)。</summary>
        public static Tuple<int, int> ImportCsv(string filePath, IList<WordEntry> words)
        {
            int added = 0;
            int updated = 0;

            foreach (var fields in ReadCsvRows(filePath))
            {
                if (fields.Count < 3)
                {
                    continue;
                }

                if (fields.Count >= 1 && fields[0] == "单词")
                {
                    // 跳过导出文件自带的表头
                    continue;
                }

                var word = (fields[0] ?? string.Empty).Trim();
                if (word.Length == 0)
                {
                    continue;
                }

                WordEntry existing = null;
                foreach (var w in words)
                {
                    if (string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase))
                    {
                        existing = w;
                        break;
                    }
                }

                if (existing == null)
                {
                    var entry = new WordEntry();
                    FillFromCsv(entry, fields);
                    entry.AddedDate = DateTime.Now;
                    words.Add(entry);
                    added++;
                }
                else
                {
                    // 只补充空白字段，不覆盖已填内容
                    if (string.IsNullOrWhiteSpace(existing.Phonetics)) existing.Phonetics = Field(fields, 1);
                    if (string.IsNullOrWhiteSpace(existing.Meaning)) existing.Meaning = Field(fields, 2);
                    if (string.IsNullOrWhiteSpace(existing.Example)) existing.Example = Field(fields, 3);
                    if (string.IsNullOrWhiteSpace(existing.Category)) existing.Category = Field(fields, 4);
                    if (string.IsNullOrWhiteSpace(existing.Note)) existing.Note = Field(fields, 5);
                    updated++;
                }
            }

            return Tuple.Create(added, updated);
        }

        private static void FillFromCsv(WordEntry entry, List<string> fields)
        {
            entry.Word = Field(fields, 0);
            entry.Phonetics = Field(fields, 1);
            entry.Meaning = Field(fields, 2);
            entry.Example = Field(fields, 3);
            entry.Category = Field(fields, 4);
            entry.Note = Field(fields, 5);

            int proficiency;
            if (fields.Count > 6 && int.TryParse(fields[6], out proficiency))
            {
                if (proficiency < 0) proficiency = 0;
                if (proficiency > 5) proficiency = 5;
                entry.Proficiency = proficiency;
            }
        }

        private static string Field(List<string> fields, int index)
        {
            return index < fields.Count ? (fields[index] ?? string.Empty).Trim() : string.Empty;
        }

        private static IEnumerable<List<string>> ReadCsvRows(string filePath)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("无法读取 CSV 文件：" + ex.Message, ex);
            }

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0)
                {
                    continue;
                }
                yield return ParseCsvLine(line);
            }
        }

        /// <summary>解析单行 CSV：支持双引号包裹与 "" 转义（导出侧已把换行替换为"；"，无需处理跨行字段）。</summary>
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var builder = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            builder.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(builder.ToString());
                    builder.Length = 0;
                }
                else
                {
                    builder.Append(c);
                }
            }

            fields.Add(builder.ToString());
            return fields;
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string ReplaceNewline(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace("\r\n", "；").Replace("\r", "；").Replace("\n", "；");
        }
    }
}
