// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Security.Cryptography;

namespace AStudio.App.Services;

public static class ContentHash
{
    /// <summary>SHA-256 hex (lowercase) of file bytes, or null if missing/unreadable.</summary>
    public static string? Sha256File(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
