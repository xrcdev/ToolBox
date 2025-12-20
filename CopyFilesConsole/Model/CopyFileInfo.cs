namespace CopyFilesConsole.Model
{
    public class CopyFileInfo
    {
        public DateTime CreateTime { get; set; }
        public string FileDir { get; set; }
        public string RelateDir { get; set; }
        public string FileName { get; set; }
        public string FileExt { get; set; }
        public string FileFullName { get; set; }
        public bool IsPdbExists { get; set; }
    }
}