using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyFilesConsole.Model
{
    internal class CopyFileConfig
    {
        public string FromDir { get; set; } = string.Empty;
        public string ToDir { get; set; } = string.Empty;
        public DateTime AfterTime { get; set; }
        public bool IsCopyPdb { get; set; }
    }
}
