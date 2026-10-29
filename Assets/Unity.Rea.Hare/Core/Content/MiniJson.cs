using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Unity.Hare.Content
{
    /// <summary>
    /// A small, dependency-free JSON reader for the content data files. Core has no engine reference
    /// (AGENTS.md rule 2) and its rules run under plain `dotnet test`, so parsing can't lean on
    /// UnityEngine.JsonUtility (engine-only) or a NuGet package neither side already references; this is
    /// the one place that understands the text format. Produces a plain object tree: <see cref="Dictionary{TKey,TValue}"/>
    /// (string keys) for objects, <see cref="List{T}"/> for arrays, <see cref="string"/>, <see cref="double"/>,
    /// <see cref="bool"/>, or null for scalars &#8212; the shapes <see cref="JsonNode"/> reads from.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON text is null.");
            int i = 0;
            SkipWhitespace(json, ref i);
            if (i >= json.Length) throw new FormatException("JSON text is empty.");
            object value = ParseValue(json, ref i);
            SkipWhitespace(json, ref i);
            if (i != json.Length) throw new FormatException($"Unexpected trailing content at position {i}.");
            return value;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON.");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't':
                    Expect(s, ref i, "true");
                    return true;
                case 'f':
                    Expect(s, ref i, "false");
                    return false;
                case 'n':
                    Expect(s, ref i, "null");
                    return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref i);
                    throw new FormatException($"Unexpected character '{c}' at position {i}.");
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++; // '{'
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == '}') { i++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != '"') throw new FormatException($"Expected a string key at position {i}.");
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != ':') throw new FormatException($"Expected ':' after key \"{key}\" at position {i}.");
                i++;
                object value = ParseValue(s, ref i);
                result[key] = value;
                SkipWhitespace(s, ref i);
                char next = Peek(s, i);
                if (next == ',') { i++; continue; }
                if (next == '}') { i++; break; }
                throw new FormatException($"Expected ',' or '}}' at position {i}.");
            }
            return result;
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // '['
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == ']') { i++; return result; }
            while (true)
            {
                object value = ParseValue(s, ref i);
                result.Add(value);
                SkipWhitespace(s, ref i);
                char next = Peek(s, i);
                if (next == ',') { i++; continue; }
                if (next == ']') { i++; break; }
                throw new FormatException($"Expected ',' or ']' at position {i}.");
            }
            return result;
        }

        static string ParseString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("Unterminated string.");
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\')
                {
                    if (i >= s.Length) throw new FormatException("Unterminated escape sequence.");
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Truncated \\u escape.");
                            string hex = s.Substring(i, 4);
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw new FormatException($"Unknown escape '\\{esc}'.");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (Peek(s, i) == '-') i++;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (Peek(s, i) == '.') { i++; while (i < s.Length && char.IsDigit(s[i])) i++; }
            if (Peek(s, i) == 'e' || Peek(s, i) == 'E')
            {
                i++;
                if (Peek(s, i) == '+' || Peek(s, i) == '-') i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            string token = s.Substring(start, i - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormatException($"Invalid number '{token}' at position {start}.");
            return value;
        }

        static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
                throw new FormatException($"Expected '{literal}' at position {i}.");
            i += literal.Length;
        }

        static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
