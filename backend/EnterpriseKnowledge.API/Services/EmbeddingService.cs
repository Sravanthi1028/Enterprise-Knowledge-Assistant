
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EnterpriseKnowledge.API.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;

        private const string ModelName = "gemini-embedding-001";

        public EmbeddingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<float>> GenerateEmbeddingAsync(
            string inputText,
            string taskType = "RETRIEVAL_DOCUMENT")
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                throw new ArgumentException(
                    "Input text cannot be empty.");
            }

            string? apiKey =
                Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Gemini API key is not configured.");
            }

            var requestBody = new
            {
                model = "models/" + ModelName,
                content = new
                {
                    parts = new[]
                    {
                        new { text = inputText }
                    }
                },
                taskType = taskType,
                outputDimensionality = 768
            };

            string json = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://generativelanguage.googleapis.com/v1beta/models/" +
                ModelName + ":embedContent"
            );

            request.Headers.Add("x-goog-api-key", apiKey);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

            using var response =
                await _httpClient.SendAsync(request);

            string responseText =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Gemini Embedding API error: " +
                    $"{response.StatusCode} - {responseText}"
                );
            }

            using JsonDocument document =
                JsonDocument.Parse(responseText);

            var values = document.RootElement
                .GetProperty("embedding")
                .GetProperty("values");

            List<float> embedding = values
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToList();

            // Normalize the 768-dimensional embedding
            double magnitude = Math.Sqrt(
                embedding.Sum(value => (double)value * value)
            );

            if (magnitude == 0)
            {
                throw new Exception(
                    "Generated embedding has zero magnitude.");
            }

            return embedding
                .Select(value => (float)(value / magnitude))
                .ToList();
        }
    }
}
