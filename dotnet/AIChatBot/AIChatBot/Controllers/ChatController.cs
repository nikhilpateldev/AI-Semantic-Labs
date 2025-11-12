using AIChatBot.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIChatBot.Controllers
{
    public class ChatController : Controller
    {
        private readonly OllamaService _ollama;
        private readonly MyDbContext _db;

        public ChatController(OllamaService ollama, MyDbContext db)
        {
            _ollama = ollama;
            _db = db;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            // Step 1: LLM intent + ID extraction
            var intentJson = await _ollama.AskModelAsync($@"
Classify this user message: '{request.Message}'.

Return only valid JSON, no explanation, no markdown.

Schema:
{{
  ""intent"": ""status_check | faq | greeting | other"",
  ""ids"": [
    {{ ""type"": ""tenant|application"", ""value"": ""<number only>"" }}
  ]
}}

Rules:
- If the user mentions anything containing the word 'tenant' (e.g., 'tenant', 'tenant number', 'LA tenant number', 'HL TenantNumber', 'LetAlliance TenantNumber'), normalize it to type = ""tenant"".
- If it's an application ID, use type = ""application"".
- Always extract only the **numeric part** of the ID (digits only).  
  Example: 'LA-TenantNumber 2' → value = ""2""  
           'tenant 12 and 15' → [{{""type"":""tenant"",""value"":""12""}}, {{""type"":""tenant"",""value"":""15""}}]
- If multiple IDs are present, return each as a separate object.
- If no IDs are present, return an empty array.
- Do not include extra text, just the JSON object.
");

            var intentData = Newtonsoft.Json.JsonConvert.DeserializeObject<IntentResult>(intentJson);

            // Step 2: Regex fallback
            if (intentData.Ids == null || !intentData.Ids.Any())
            {
                var ids = new List<IdEntity>();
                var tenantMatches = Regex.Matches(request.Message, @"\b\d{5}\b");
                ids.AddRange(tenantMatches.Cast<Match>().Select(m => new IdEntity { Type = "tenant", Value = m.Value }));

                var appMatches = Regex.Matches(request.Message, @"\b[A-Z]{1,3}\d{3,10}\b");
                ids.AddRange(appMatches.Cast<Match>().Select(m => new IdEntity { Type = "application", Value = m.Value }));

                intentData.Ids = ids;
            }

            string reply;

            if (intentData.Intent == "status_check" && intentData.Ids.Any())
            {
                // Step 3: DB queries
                var tenantIds = intentData.Ids.Where(i => i.Type.Contains("tenant")).Select(i => i.Value).ToList();
                var appIds = intentData.Ids.Where(i => i.Type == "application").Select(i => i.Value).ToList();

                var tenantResults = await _db.Tenant
                    .Where(t => tenantIds.Contains(t.TenantId.ToString()))
                    .Select(t => new { Type = "tenant", Id = t.TenantId, t.Status, t.UpdatedOn })
                    .ToListAsync();

                var appResults = await _db.Application
                    .Where(a => appIds.Contains(a.ApplicationId.ToString()))
                    .Select(a => new { Type = "application", Id = a.ApplicationId, a.Status, a.UpdatedOn })
                    .ToListAsync();

                var allResults = tenantResults.Concat(appResults);

                // Step 4: Format response
                string dbAnswer = allResults.Any()
                    ? string.Join("\n", allResults.Select(r =>
                        $"{r.Type} {r.Id}: {r.Status} (last updated {r.UpdatedOn})"))
                    : "No matching tenants or applications found.";

                reply = await _ollama.AskModelAsync(
                    $"User asked: '{request.Message}'. " +
                    $"Database results:\n{dbAnswer}\n" +
                    "You are Barbon Insurance Ltd help bot so reply politely, clearly separating tenants and applications also don't need any json in response. It should be simple text");
            }
            else if (intentData.Intent == "greeting")
            {
                reply = await _ollama.AskModelAsync(
                   "Reply politely, to the user greeting as a barbon insurance company. Tell them we are happy to help you.");
            }
            else
            {
                reply = await _ollama.AskModelAsync(
                    $"User said: '{request.Message}'. You are Barbon insurance help bot. Respond as a helpful tenant assistant.");
            }
            //var response = JsonConvert.DeserializeObject<ModelResponse>(reply);
            return Ok(reply);
        }

        // DTOs
        public class IdEntity
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }
        }

        public class IntentResult
        {
            [JsonProperty("intent")]
            public string Intent { get; set; }

            [JsonProperty("ids")]
            public List<IdEntity> Ids { get; set; }
        }


        public class ChatRequest
        {
            public string Message { get; set; }
        }
    }
    public class OllamaService
    {
        private readonly HttpClient _httpClient;

        public OllamaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:11434/");
        }

        public async Task<string> AskModelAsync(string prompt)
        {
            var request = new
            {
                model = "llama3",
                prompt = prompt,
                Stream = false
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("api/generate", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();

            // Ollama streams multiple JSON lines, so extract text only
            using var doc = JsonDocument.Parse(result);
            return doc.RootElement.GetProperty("response").GetString();
        }
    }
    public class ModelResponse
    {
        public string reply { get; set; }
    }
}
