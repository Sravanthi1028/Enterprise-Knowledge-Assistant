
using System;
using System.IO;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

namespace EnterpriseKnowledge.API.Services
{
    public class DocumentTextExtractor
    {
        public async Task<string> ExtractTextAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Document file not found.", filePath);
            }

            string extension = Path.GetExtension(filePath)
                .ToLowerInvariant();

            switch (extension)
            {
                case ".txt":
                    return await File.ReadAllTextAsync(filePath);

                case ".pdf":
                    return ExtractPdfText(filePath);

                case ".docx":
                    return ExtractWordText(filePath);

                default:
                    throw new NotSupportedException(
                        "Only TXT, PDF and DOCX files are supported.");
            }
        }

        private string ExtractPdfText(string filePath)
        {
            using (var pdf = PdfDocument.Open(filePath))
            {
                var text = new System.Text.StringBuilder();

                foreach (var page in pdf.GetPages())
                {
                    text.AppendLine(page.Text);
                }

                return text.ToString();
            }
        }

        private string ExtractWordText(string filePath)
        {
            using (var document =
                WordprocessingDocument.Open(filePath, false))
            {
                return document.MainDocumentPart?
    .Document?.Body?.InnerText ?? string.Empty;
            }
        }
    }
}
