namespace CopyFilesConsole.Model
{
    public class CopyFileInfo
    {
        public DateTime CreateTime { get; set; }
        public string FileDir { get; set; } = string.Empty;
        public string RelateDir { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileExt { get; set; } = string.Empty;
        public string FileFullName { get; set; } = string.Empty;
        public bool IsPdbExists { get; set; }
    }
}
