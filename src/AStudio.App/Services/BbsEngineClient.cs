// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AStudio.App.Services;

/// <summary>
/// Thin P/Invoke to AQC <c>bbs_engine.dll</c> (S2d). Numbers stay in the C++ SoT —
/// AStudio only displays smoke / summary output. Full BBS UI remains in AQC BBSApp.
/// </summary>
public sealed class GenTable
{
    [JsonPropertyName("headers")] public List<string> Headers { get; set; } = new();
    [JsonPropertyName("rows")] public List<List<string>> Rows { get; set; } = new();
}

public sealed class GenResult
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("bbs")] public GenTable Bbs { get; set; } = new();
    [JsonPropertyName("summary")] public GenTable Summary { get; set; } = new();
    [JsonPropertyName("checks")] public GenTable Checks { get; set; } = new();
}

public static class BbsEngineClient
{
    private const string Dll = "bbs_engine.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void bbs_free(IntPtr p);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int bbs_generate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string kind,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? settingsJson,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? rowsJson,
        out IntPtr outJson);

    public static bool DllPresent()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, Dll);
        return File.Exists(beside);
    }

    public static string? DllPath()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, Dll);
        return File.Exists(beside) ? beside : null;
    }

    private static string? PtrToUtf8(IntPtr p)
    {
        if (p == IntPtr.Zero) return null;
        try
        {
            var len = 0;
            while (Marshal.ReadByte(p, len) != 0) len++;
            var bytes = new byte[len];
            Marshal.Copy(p, bytes, 0, len);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            bbs_free(p);
        }
    }

    public static GenResult Generate(string kind, JsonObject settings, IEnumerable<Dictionary<string, string>> rows)
    {
        var rowsArr = new JsonArray();
        foreach (var row in rows)
        {
            var o = new JsonObject();
            foreach (var kv in row) o[kv.Key] = kv.Value;
            rowsArr.Add(o);
        }
        var settingsJson = settings.ToJsonString();
        var rowsJson = rowsArr.ToJsonString();
        var rc = bbs_generate(kind, settingsJson, rowsJson, out var ptr);
        var text = PtrToUtf8(ptr) ?? "{\"ok\":false,\"error\":\"Empty response\"}";
        var result = JsonSerializer.Deserialize<GenResult>(text) ?? new GenResult { Ok = false, Error = "Bad JSON" };
        if (rc == 0)
        {
            result.Ok = false;
            if (string.IsNullOrWhiteSpace(result.Error))
                result.Error = "Generate failed.";
        }
        return result;
    }

    /// <summary>Minimal rectangular column smoke — proves in-process P/Invoke from Focus.</summary>
    public static GenResult SmokeColumn()
    {
        var settings = new JsonObject
        {
            ["diameters"] = new JsonArray(8, 10, 12, 16, 20, 25),
            ["hook_allowance"] = new JsonObject { ["90"] = 9, ["135"] = 10, ["180"] = 16 },
            ["bend_deduction"] = new JsonObject { ["45"] = 1, ["90"] = 2, ["135"] = 3 },
            ["hysd_bond"] = 1,
            ["hysd_bond_factor"] = 1.6,
            ["min_hook_mm"] = 75,
            ["covers"] = new JsonObject
            {
                ["column"] = 40,
                ["beam"] = 25,
                ["slab"] = 20,
                ["footing"] = 50,
            },
            ["default_column_lap"] = "No",
            ["tau_bd"] = new JsonObject
            {
                ["M20"] = 1.2, ["M25"] = 1.4, ["M30"] = 1.5, ["M35"] = 1.7, ["M40"] = 1.9
            },
            ["fy"] = new JsonObject
            {
                ["Fe250"] = 250, ["Fe415"] = 415, ["Fe500"] = 500, ["Fe550"] = 550
            },
        };

        var row = new Dictionary<string, string>
        {
            ["mark"] = "C1",
            ["nos"] = "1",
            ["level"] = "Lvl0",
            ["width"] = "300",
            ["depth"] = "450",
            ["height"] = "3000",
            ["cover"] = "40",
            ["concrete_grade"] = "M25",
            ["column_type"] = "Rectangular",
            ["stirrup_dia"] = "8",
            ["spacing"] = "150",
            ["hook_angle"] = "135",
            ["tie_type"] = "Auto",
            ["bars"] = "16:8",
            ["steel_grade"] = "Fe500",
            ["provide_lap"] = "No",
        };

        return Generate("columns", settings, new[] { row });
    }

    public static string FormatSmokeSummary(GenResult res)
    {
        if (!res.Ok)
            return $"Engine error: {res.Error ?? "unknown"}";
        var bbsRows = res.Bbs.Rows?.Count ?? 0;
        var summaryRows = res.Summary.Rows?.Count ?? 0;
        var first = "";
        if (res.Summary.Rows is { Count: > 0 } && res.Summary.Headers is { Count: > 0 })
        {
            var cells = res.Summary.Rows[0];
            first = string.Join(" · ", res.Summary.Headers.Zip(cells, (h, c) => $"{h}={c}").Take(4));
        }
        return string.IsNullOrEmpty(first)
            ? $"ok — BBS rows={bbsRows}, summary rows={summaryRows}"
            : $"ok — BBS rows={bbsRows}; {first}";
    }
}
