// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;

namespace AStudio.App.Services;

/// <summary>India money helpers — store integer paise; display INR.</summary>
public static class MoneyPaise
{
    public static string FormatInr(long paise)
    {
        var rupees = paise / 100.0;
        return "₹" + rupees.ToString("N2", CultureInfo.GetCultureInfo("en-IN"));
    }

    /// <summary>Parse rupees text (e.g. 125000 or 1,25,000.50) → paise. Returns false on bad input.</summary>
    public static bool TryParseRupees(string? text, out long paise)
    {
        paise = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Trim()
            .Replace("₹", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var rupees))
            return false;
        if (rupees < 0) return false;
        paise = (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
