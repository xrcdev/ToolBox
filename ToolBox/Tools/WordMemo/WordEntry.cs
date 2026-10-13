using System;
using System.Xml.Serialization;

namespace ToolBox.Tools.WordMemo
{
    /// <summary>单个单词条目。移植自 WordMemo.WordEntry（.NET FW 4.6.2 老项目），
    /// 属性名与原 XML 结构一致，%LocalAppData%\WordMemo\words.xml 旧数据可直接读取。</summary>
    public class WordEntry
    {
        public string Word { get; set; } = string.Empty;

        public string Phonetics { get; set; } = string.Empty;

        public string Meaning { get; set; } = string.Empty;

        public string Example { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        public DateTime AddedDate { get; set; } = DateTime.Now;

        /// <summary>熟练度 0-5：复习答对 +1，忘记清零。</summary>
        public int Proficiency { get; set; }

        public int ReviewCount { get; set; }

        public DateTime? LastReviewDate { get; set; }

        /// <summary>列表展示用：★ 掌握程度。XmlSerializer 不序列化只读属性。</summary>
        [XmlIgnore]
        public string ProficiencyDisplay
        {
            get
            {
                var p = Proficiency;
                if (p < 0) p = 0;
                if (p > 5) p = 5;
                return new string('★', p) + new string('☆', 5 - p);
            }
        }
    }
}
