using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyFilesConsole.Model
{
    internal class CopyFileConfig
    {
        public string FromDir { get; set; }
        public string ToDir { get; set; }
        public DateTime AfterTime { get; set; }
        public bool IsCopyPdb { get; set; }
    }
}
