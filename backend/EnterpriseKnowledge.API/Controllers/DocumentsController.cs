
using EnterpriseKnowledge.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EnterpriseKnowledge.API.Data;
using EnterpriseKnowledge.API.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledge.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly DocumentTextExtractor _textExtractor;
        private readonly DocumentChunkingService _chunkingService;

        private const long MaxFileSize = 10 * 1024 * 1024;

        private readonly string[] _allowedExtensions =
        {
            ".pdf",
            ".docx",
            ".txt"
        };

        public DocumentsController(
            AppDbContext context,
            IWebHostEnvironment environment,
            DocumentTextExtractor textExtractor,
            DocumentChunkingService chunkingService)
        {
            _context = context;
            _environment = environment;
            _textExtractor = textExtractor;
            _chunkingService = chunkingService;
        }

        // POST: api/Documents/upload
        [HttpPost("upload")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> UploadDocument(
            IFormFile file,
            [FromForm] string title)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Please select a file."
                });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new
                {
                    message = "File size must not exceed 10 MB."
                });
            }

            string extension =
                Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!_allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    message = "Only PDF, DOCX and TXT files are allowed."
                });
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest(new
                {
                    message = "Document title is required."
                });
            }

            string? userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            string uploadFolder = Path.Combine(
                _environment.ContentRootPath,
                "Uploads",
                "Documents"
            );

            Directory.CreateDirectory(uploadFolder);

            string uniqueFileName =
                $"{Guid.NewGuid()}{extension}";

            string fullPath = Path.Combine(
                uploadFolder,
                uniqueFileName
            );

            using (var stream = new FileStream(
                fullPath,
                FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = new Document
            {
                Title = title.Trim(),
                FileName = Path.GetFileName(file.FileName),
                FilePath = Path.Combine(
                    "Uploads",
                    "Documents",
                    uniqueFileName
                ),
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedByUserId = userId,
                ProcessingStatus = "Pending"
            };

            try
            {
                document.ExtractedText =
                    await _textExtractor.ExtractTextAsync(fullPath);

                document.ProcessingStatus = "Completed";
            }
            catch (Exception)
            {
                document.ProcessingStatus = "Failed";
            }

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            int totalChunks = 0;

            if (document.ProcessingStatus == "Completed" &&
                !string.IsNullOrWhiteSpace(document.ExtractedText))
            {
                var chunks =
                    await _chunkingService.CreateChunksWithEmbeddingsAsync(
                        document.ExtractedText,
                        document.DocumentId
                    );

                if (chunks.Count > 0)
                {
                    _context.DocumentChunks.AddRange(chunks);
                    await _context.SaveChangesAsync();

                    totalChunks = chunks.Count;
                }
            }

            return Ok(new
            {
                message = "Document uploaded and embeddings generated successfully.",
                document = new
                {
                    document.DocumentId,
                    document.Title,
                    document.FileName,
                    document.ContentType,
                    document.FileSize,
                    document.UploadedAt,
                    document.ProcessingStatus,
                    ExtractedTextLength =
                        document.ExtractedText?.Length ?? 0,
                    TotalChunks = totalChunks,
                    EmbeddingsGenerated = totalChunks
                }
            });
        }

        // GET: api/Documents
        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var documents = await _context.Documents
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    d.DocumentId,
                    d.Title,
                    d.FileName,
                    d.ContentType,
                    d.FileSize,
                    d.UploadedAt,
                    d.ProcessingStatus
                })
                .ToListAsync();

            return Ok(documents);
        }

        // GET: api/Documents/admin/all
        // Admin-only endpoint
        [Authorize(Roles = "Admin")]
        [HttpGet("admin/all")]
        public async Task<IActionResult> GetAllDocumentsForAdmin()
        {
            var documents = await _context.Documents
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    d.DocumentId,
                    d.Title,
                    d.FileName,
                    d.ContentType,
                    d.FileSize,
                    d.UploadedAt,
                    d.ProcessingStatus,
                    d.UploadedByUserId
                })
                .ToListAsync();

            return Ok(new
            {
                totalDocuments = documents.Count,
                documents
            });
        }

        // DELETE: api/Documents/admin/7
        // Admin-only document deletion
        [Authorize(Roles = "Admin")]
        [HttpDelete("admin/{documentId}")]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            string fullPath = Path.GetFullPath(
                Path.Combine(
                    _environment.ContentRootPath,
                    document.FilePath
                )
            );

            string uploadFolder = Path.GetFullPath(
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "Documents"
                )
            );

            // Ensure the file belongs to the document upload folder.
            string uploadFolderPrefix =
                uploadFolder.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ) + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(
                uploadFolderPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Invalid document file path."
                });
            }

            // Delete the physical file first.
            if (System.IO.File.Exists(fullPath))
            {
                try
                {
                    System.IO.File.Delete(fullPath);
                }
                catch (IOException)
                {
                    return StatusCode(500, new
                    {
                        message = "Unable to delete the document file. Please try again."
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    return StatusCode(500, new
                    {
                        message = "Permission denied while deleting the document file."
                    });
                }
            }

            // Delete database record.
            // Associated chunks and embeddings are deleted by Cascade Delete.
            _context.Documents.Remove(document);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Document and associated chunks deleted successfully.",
                deletedDocumentId = documentId,
                deletedFileName = document.FileName
            });
        }
    }
}
