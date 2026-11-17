
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using EnterpriseKnowledge.API.Models;

namespace EnterpriseKnowledge.API.Services
{
    public class DocumentChunkingService
    {
        private readonly EmbeddingService _embeddingService;

        private const int ChunkSize = 1000;
        private const int OverlapSize = 150;

        public DocumentChunkingService(
            EmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
        }

        public List<DocumentChunk> CreateChunks(
            string extractedText,
            int documentId)
        {
            var chunks = new List<DocumentChunk>();

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return chunks;
            }

            int start = 0;
            int chunkIndex = 0;

            while (start < extractedText.Length)
            {
                int end = Math.Min(
                    start + ChunkSize,
                    extractedText.Length
                );

                if (end < extractedText.Length)
                {
                    int breakPoint = extractedText.LastIndexOf(
                        ' ',
                        end - 1,
                        end - start
                    );

                    if (breakPoint > start + ChunkSize / 2)
                    {
                        end = breakPoint;
                    }
                }

                string content = extractedText
                    .Substring(start, end - start)
                    .Trim();

                if (!string.IsNullOrWhiteSpace(content))
                {
                    chunks.Add(new DocumentChunk
                    {
                        DocumentId = documentId,
                        ChunkIndex = chunkIndex,
                        Content = content,
                        CreatedAt = DateTime.UtcNow
                    });

                    chunkIndex++;
                }

                if (end >= extractedText.Length)
                {
                    break;
                }

                start = Math.Max(
                    start + 1,
                    end - OverlapSize
                );
            }

            return chunks;
        }

        // Generate embeddings for all document chunks
        public async Task<List<DocumentChunk>>
            CreateChunksWithEmbeddingsAsync(
                string extractedText,
                int documentId)
        {
            var chunks = CreateChunks(
                extractedText,
                documentId
            );

            foreach (var chunk in chunks)
            {
                var embedding =
                    await _embeddingService.GenerateEmbeddingAsync(
                        chunk.Content,
                        "RETRIEVAL_DOCUMENT"
                    );

                chunk.Embedding =
                    JsonSerializer.Serialize(embedding);
            }

            return chunks;
        }
    }
}
