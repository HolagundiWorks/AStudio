// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Net.Http.Json;
using System.Text.Json;

namespace AStudio.App.Services;

/// <summary>
/// Desktop-only ESTI → local Ollama (S4). Never called against the hub/VPS.
/// Transcripts stay on-device — do not enqueue to sync outbox.
/// </summary>
public sealed class EstiOllamaClient : IDisposable
{
    readonly HttpClient _http;
    readonly string _model;

    public EstiOllamaClient(string? baseUrl = null, string? model = null)
    {
        var url = (baseUrl
            ?? Environment.GetEnvironmentVariable("ESTI_OLLAMA_URL")
            ?? "http://127.0.0.1:11434").TrimEnd('/');
        _model = (model
            ?? Environment.GetEnvironmentVariable("ESTI_OLLAMA_MODEL")
            ?? "llama3.2").Trim();
        _http = new HttpClient
        {
            BaseAddress = new Uri(url + "/"),
            Timeout = TimeSpan.FromSeconds(90),
        };
    }

    public string BaseUrl => _http.BaseAddress?.ToString()?.TrimEnd('/') ?? "";
    public string Model => _model;

    public async Task<EstiProbeResult> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync("api/tags", ct);
            if (!res.IsSuccessStatusCode)
            {
                return new EstiProbeResult(false, _model, Array.Empty<string>(),
                    $"HTTP {(int)res.StatusCode} from {BaseUrl}");
            }
            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var names = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var models) &&
                models.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in models.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var n))
                        names.Add(n.GetString() ?? "");
                }
            }
            names.RemoveAll(string.IsNullOrWhiteSpace);
            var hasModel = names.Any(n =>
                n.Equals(_model, StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith(_model + ":", StringComparison.OrdinalIgnoreCase));
            var note = hasModel
                ? $"Ollama OK · model {_model} present ({names.Count} tagged)"
                : names.Count == 0
                    ? $"Ollama reachable · no models — pull {_model}"
                    : $"Ollama OK · {_model} not tagged (have: {string.Join(", ", names.Take(4))})";
            return new EstiProbeResult(true, _model, names, note);
        }
        catch (Exception ex)
        {
            return new EstiProbeResult(false, _model, Array.Empty<string>(),
                $"Ollama unreachable at {BaseUrl}: {ex.Message}");
        }
    }

    /// <summary>Mission-style ask: short answer for principal (where · matters · do · next).</summary>
    public async Task<EstiAskResult> AskAsync(
        string userText,
        string? projectContext,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return new EstiAskResult(false, "", "Enter a question for ESTI.");

        var system = """
            You are ESTI, the local practice assistant inside AStudio (AORMS).
            Answer for an architecture principal under time pressure.
            In ≤120 words cover: where we are · what matters · what to do now · what next.
            Use Indian practice language when relevant (GST, COA, phases). Never invent money or quantities — those come from bbs_engine / AQC.
            Do not mention being an AI model unless asked. Desktop-only: no cloud hub claims.
            """;

        var user = string.IsNullOrWhiteSpace(projectContext)
            ? userText.Trim()
            : $"Project context:\n{projectContext.Trim()}\n\nQuestion:\n{userText.Trim()}";

        var body = new
        {
            model = _model,
            stream = false,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
            options = new { temperature = 0.3 },
        };

        try
        {
            using var res = await _http.PostAsJsonAsync("api/chat", body, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return new EstiAskResult(false, "", $"Ollama chat HTTP {(int)res.StatusCode}: {Trim(raw, 240)}");

            using var doc = JsonDocument.Parse(raw);
            var reply = doc.RootElement.TryGetProperty("message", out var msg) &&
                        msg.TryGetProperty("content", out var content)
                ? content.GetString() ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(reply))
                return new EstiAskResult(false, "", "Empty reply from Ollama.");
            return new EstiAskResult(true, reply.Trim(), $"model={_model}");
        }
        catch (TaskCanceledException)
        {
            return new EstiAskResult(false, "", "Ollama timed out — is the model loaded?");
        }
        catch (Exception ex)
        {
            return new EstiAskResult(false, "", $"Ask failed: {ex.Message}");
        }
    }

    static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    public void Dispose() => _http.Dispose();
}

public sealed record EstiProbeResult(
    bool Reachable,
    string Model,
    IReadOnlyList<string> TaggedModels,
    string Note);

public sealed record EstiAskResult(bool Ok, string Reply, string Note);
