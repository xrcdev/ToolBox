using iTextSharp.text.pdf;
using iTextSharp.text;
using System.IO;

namespace PdfSplitter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: PdfSplitter <inputPdfPath> <splitCount> [outputDir]");
                return;
            }

            var inputFilePath = args[0];
            if (!int.TryParse(args[1], out int splitNum) || splitNum <= 0)
            {
                Console.WriteLine("Error: splitCount must be a positive integer.");
                return;
            }

            var outDir = args.Length >= 3 ? args[2] : Path.GetDirectoryName(inputFilePath) ?? ".";

            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine($"Error: File not found: {inputFilePath}");
                return;
            }

            Directory.CreateDirectory(outDir);

            var fileName = Path.GetFileNameWithoutExtension(inputFilePath);

            using var reader = new PdfReader(inputFilePath);
            int totalPages = reader.NumberOfPages;
            int pageSize = (int)Math.Round((double)totalPages / splitNum, MidpointRounding.AwayFromZero);

            Console.WriteLine($"Total pages: {totalPages}, Split into {splitNum} parts (~{pageSize} pages each)");

            int addPageCount = 1;
            for (int i = 1; i <= splitNum; i++)
            {
                int remainingPages = totalPages - addPageCount + 1;
                int thisTimeNum = Math.Min(pageSize, remainingPages);

                if (thisTimeNum <= 0) break;

                var outputPath = Path.Combine(outDir, $"{fileName}_{i}.pdf");
                using var stream = new FileStream(outputPath, FileMode.Create);
                var document = new Document();
                var pdf = new PdfCopy(document, stream);

                document.Open();
                for (int j = 0; j < thisTimeNum; j++)
                {
                    document.NewPage();
                    PdfImportedPage page = pdf.GetImportedPage(reader, addPageCount);
                    pdf.AddPage(page);
                    addPageCount++;
                }
                document.Close();
                pdf.Close();

                Console.WriteLine($"Created: {outputPath} ({thisTimeNum} pages)");
            }

            Console.WriteLine("Done.");
        }
    }
}
