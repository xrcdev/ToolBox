using iTextSharp.text.pdf;
using iTextSharp.text;
using System.IO;

namespace PdfSplitter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var inputFilePath = "C:\\Users\\Richard\\Downloads\\NET框架设计 模式、配置、工具\\NET框架设计 模式、配置、工具.pdf";
            var outDir = "C:\\Users\\Richard\\Downloads\\NET框架设计 模式、配置、工具\\";
            double splitNum = 2;
            var fileName = Path.GetFileNameWithoutExtension(inputFilePath);
            PdfReader reader = new PdfReader(inputFilePath);
            int totalPages = reader.NumberOfPages;
            int pageSize = (int)Math.Round(totalPages / splitNum, 0);

            var addPageCount = 1;
            for (int i = 1; i <= splitNum + 1; i++)
            {
                var thisTimeNum = (i + 1) * pageSize >= totalPages ? totalPages - i * pageSize : pageSize;
                Document document = new Document();
                var stream = new FileStream(Path.Combine(outDir, fileName + "_" + i + ".pdf"), FileMode.Create);
                PdfCopy pdf = new PdfCopy(document, stream);
                document.Open();
                for (int j = 1; j <= thisTimeNum; j++)
                {
                    document.NewPage();
                    PdfImportedPage page = pdf.GetImportedPage(reader, addPageCount);
                    pdf.AddPage(page);
                    addPageCount += 1;
                }
                //document.Close();
                //pdf.Close();
                //stream.Close();
            }

            reader.Close();
        }
    }
}
