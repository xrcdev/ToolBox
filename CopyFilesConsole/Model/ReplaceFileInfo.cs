namespace CopyFilesConsole.Model
{
    public class ReplaceFileInfo
    {
        public CopyFileInfo newFile { get; set; }
        public CopyFileInfo targetFile { get; set; }
        public bool ReplaceSuccess { get; set; }
    }
}