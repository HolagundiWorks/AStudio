// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;
using System.Text.RegularExpressions;

namespace AStudio.App.Services;

/// <summary>
/// Floating office calculator — bare numbers are metres; + − × ÷ ( ).
/// Peer to web FloatingCalculator (narrower: no unit tokens yet).
/// </summary>
public static class OfficeCalculator
{
    const double MPerFt = 0.3048;
    const double MPerIn = 0.0254;

    public static double? Eval(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = Normalize(input);
        if (string.IsNullOrEmpty(s)) return null;
        try
        {
            var tokens = Tokenize(s);
            if (tokens is null) return null;
            var rpn = ToRpn(tokens);
            if (rpn is null) return null;
            return EvalRpn(rpn);
        }
        catch
        {
            return null;
        }
    }

    public static string Format(double meters, bool imperial)
    {
        if (!imperial)
            return $"{meters.ToString("0.####", CultureInfo.InvariantCulture)} m";

        var totalIn = meters / MPerIn;
        var neg = totalIn < 0;
        totalIn = Math.Abs(totalIn);
        var ft = (int)Math.Floor(totalIn / 12.0);
        var inches = totalIn - ft * 12;
        var sign = neg ? "-" : "";
        if (Math.Abs(inches) < 0.05)
            return $"{sign}{ft}'0\"";
        return $"{sign}{ft}'{inches.ToString("0.#", CultureInfo.InvariantCulture)}\"";
    }

    static string Normalize(string input) =>
        Regex.Replace(input.Trim().Replace('×', '*').Replace('÷', '/').Replace('−', '-'), @"\s+", "");

    static List<string>? Tokenize(string s)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c is '+' or '*' or '/' or '(' or ')')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }
            if (c == '-')
            {
                var unary = tokens.Count == 0
                    || tokens[^1] is "(" or "+" or "-" or "*" or "/";
                if (unary)
                {
                    i++;
                    if (i >= s.Length || !char.IsDigit(s[i]) && s[i] != '.') return null;
                    var start = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    tokens.Add("-" + s[start..i]);
                }
                else
                {
                    tokens.Add("-");
                    i++;
                }
                continue;
            }
            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                tokens.Add(s[start..i]);
                continue;
            }
            return null;
        }
        return tokens;
    }

    static List<string>? ToRpn(List<string> tokens)
    {
        var outQ = new List<string>();
        var ops = new Stack<string>();
        int Prec(string op) => op is "*" or "/" ? 2 : 1;

        foreach (var t in tokens)
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                outQ.Add(t);
                continue;
            }
            if (t is "+" or "-" or "*" or "/")
            {
                while (ops.Count > 0 && ops.Peek() is not "(" && Prec(ops.Peek()) >= Prec(t))
                    outQ.Add(ops.Pop());
                ops.Push(t);
                continue;
            }
            if (t == "(")
            {
                ops.Push(t);
                continue;
            }
            if (t == ")")
            {
                while (ops.Count > 0 && ops.Peek() != "(")
                    outQ.Add(ops.Pop());
                if (ops.Count == 0 || ops.Pop() != "(") return null;
                continue;
            }
            return null;
        }
        while (ops.Count > 0)
        {
            var op = ops.Pop();
            if (op is "(" or ")") return null;
            outQ.Add(op);
        }
        return outQ;
    }

    static double? EvalRpn(List<string> rpn)
    {
        var st = new Stack<double>();
        foreach (var t in rpn)
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            {
                st.Push(n);
                continue;
            }
            if (st.Count < 2) return null;
            var b = st.Pop();
            var a = st.Pop();
            st.Push(t switch
            {
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" => b == 0 ? double.NaN : a / b,
                _ => double.NaN,
            });
            if (double.IsNaN(st.Peek())) return null;
        }
        return st.Count == 1 ? st.Pop() : null;
    }
}
