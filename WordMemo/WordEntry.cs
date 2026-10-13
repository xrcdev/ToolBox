using System;
using System.Xml.Serialization;

namespace WordMemo
{
    /// <summary>单个单词条目。XmlSerializer 要求公共无参构造与可读可写属性。</summary>
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
