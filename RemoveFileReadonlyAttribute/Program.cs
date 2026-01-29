using System;
using System.IO;

namespace RemoveFileReadonlyAttribute
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please enter the directory path:");
            string? path = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Console.WriteLine("Invalid directory path or directory does not exist.");
                return;
            }

            try
            {
                Console.WriteLine($"Processing directory: {path}");
                ProcessDirectory(path);
                Console.WriteLine("Completed removing ReadOnly attributes.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void ProcessDirectory(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory );
            foreach (string fileName in fileEntries)
            {
                RemoveReadOnlyAttribute(fileName);
            }

            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
            {
                ProcessDirectory(subdirectory);
            }
        }

        static void RemoveReadOnlyAttribute(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.IsReadOnly)
                {
                    fileInfo.IsReadOnly = false;
                    Console.WriteLine($"Removed ReadOnly attribute: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process {filePath}: {ex.Message}");
            }
        }
    }
}
