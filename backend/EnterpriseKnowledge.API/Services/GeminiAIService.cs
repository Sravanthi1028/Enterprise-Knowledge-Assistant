
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EnterpriseKnowledge.API.Services
{
    public class GeminiAIService
    {
        private readonly HttpClient _httpClient;

        public GeminiAIService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GenerateAnswerAsync(
            string question,
            string context)
        {
            string? apiKey =
                Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Gemini API key is not configured.");
            }

            string prompt = $@"
You are an Enterprise Knowledge Assistant.

Answer the user's question using only the provided document context.

If the answer is not available in the context,
clearly say that the uploaded documents do not contain
enough information.

DOCUMENT CONTEXT:
{context}

USER QUESTION:
{question}

Provide a clear and helpful answer.
";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            string json = JsonSerializer.Serialize(requestBody);

            HttpResponseMessage? response = null;
            string responseText = string.Empty;

            // Retry Gemini requests up to 3 times for temporary 503 errors.
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent"
                );

                request.Headers.Add("x-goog-api-key", apiKey);

                request.Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                response = await _httpClient.SendAsync(request);

                responseText =
                    await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                // Retry only when Gemini returns 503 Service Unavailable.
                if ((int)response.StatusCode != 503 || attempt == 3)
                {
                    throw new Exception(
                        $"Gemini API error: {response.StatusCode} - {responseText}"
                    );
                }

                await Task.Delay(attempt * 3000);
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                throw new Exception(
                    "Gemini API is temporarily unavailable. Please try again later."
                );
            }

            using JsonDocument document =
                JsonDocument.Parse(responseText);

            var answer = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return answer ?? "No answer was generated.";
        }
    }
}
