namespace CopyFilesConsole.Model
{
    public class ReplaceFileInfo
    {
        public CopyFileInfo NewFile { get; set; } = new();
        public CopyFileInfo TargetFile { get; set; } = new();
        public bool ReplaceSuccess { get; set; }
    }
}
