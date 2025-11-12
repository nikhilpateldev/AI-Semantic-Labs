using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using Qdrant.Client;

namespace SemanticSearchAPI.Controllers
{
    public class SearchController : Controller
    {
        private readonly HttpClient _http;
        private readonly QdrantClient _qdrant;
        private readonly IConfiguration _config;

        public SearchController(IHttpClientFactory httpFactory, QdrantClient qdrant, IConfiguration config)
        {
            _http = httpFactory.CreateClient();
            _qdrant = qdrant;
            _config = config;
        }


        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] QueryRequest req)
        {
            // Step 1️⃣ — Generate embeddings from Ollama
            var embedUrl = _config.GetValue<string>("Ollama:EmbedUrl") ?? "http://localhost:11434/api/embed";
            var model = _config.GetValue<string>("Ollama:Model") ?? "nomic-embed-text";

            var embedReq = new { model = model, input = req.Query };
            var json = JsonSerializer.Serialize(embedReq);

            using var resp = await _http.PostAsync(embedUrl, new StringContent(json, Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);

            // Step 2️⃣ — Extract the embedding vector
            var vectorElem = doc.RootElement.GetProperty("embeddings")[0];
            List<float> vec = new();
            foreach (var item in vectorElem.EnumerateArray()) vec.Add(item.GetSingle());

            // Step 3️⃣ — Query Qdrant using QueryAsync (per latest SDK docs)
            var response = await _qdrant.QueryAsync(
                collectionName: req.Collection ?? "my_docs",
                query: vec.ToArray()
            );

            // Step 4️⃣ — Shape response
            var outRes = response.Select(r => new
            {
                id = r.Id,
                score = r.Score,
                payload = r.Payload
            });

            return Ok(outRes);
        }
    }

    public record QueryRequest(string Query, int TopK = 5, string? Collection = null);
    public class SearchPointsRequest
    {
        public int Limit { get; set; }
        public float[] Vector { get; set; }
    }
}