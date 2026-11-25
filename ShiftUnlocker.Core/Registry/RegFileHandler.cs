using System.Text;
using Microsoft.Win32;

namespace ShiftUnlocker.Core.RegHandler;

public record RegValueEntry(string Name, object Value, RegistryValueKind Kind);
public record RegKeyEntry(string KeyPath, List<RegValueEntry> Values, bool IsDeletion = false);

public static class RegFileHandler
{
    public static string ExportToRegFormat(string keyPath, IEnumerable<RegValueEntry> values)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine($"[{keyPath}]");

        foreach (var val in values)
        {
            string valName = string.IsNullOrEmpty(val.Name) ? "@" : $"\"{val.Name.Replace("\"", "\\\"")}\"";
            switch (val.Kind)
            {
                case RegistryValueKind.DWord:
                    uint dw = Convert.ToUInt32(val.Value);
                    sb.AppendLine($"{valName}=dword:{dw:x8}");
                    break;
                case RegistryValueKind.QWord:
                    ulong qw = Convert.ToUInt64(val.Value);
                    sb.AppendLine($"{valName}=hex(b):{FormatBytes(BitConverter.GetBytes(qw))}");
                    break;
                case RegistryValueKind.Binary:
                    byte[] bytes = val.Value as byte[] ?? Array.Empty<byte>();
                    sb.AppendLine($"{valName}=hex:{FormatBytes(bytes)}");
                    break;
                case RegistryValueKind.MultiString:
                    string[] arr = val.Value as string[] ?? Array.Empty<string>();
                    byte[] msBytes = MultiStringToBytes(arr);
                    sb.AppendLine($"{valName}=hex(7):{FormatBytes(msBytes)}");
                    break;
                case RegistryValueKind.ExpandString:
                    string expStr = val.Value?.ToString() ?? "";
                    byte[] expBytes = Encoding.Unicode.GetBytes(expStr + "\0");
                    sb.AppendLine($"{valName}=hex(2):{FormatBytes(expBytes)}");
                    break;
                case RegistryValueKind.String:
                default:
                    string s = val.Value?.ToString() ?? "";
                    sb.AppendLine($"{valName}=\"{EscapeRegString(s)}\"");
                    break;
            }
        }

        return sb.ToString();
    }

    private static string EscapeRegString(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string FormatBytes(byte[] bytes)
    {
        return string.Join(",", bytes.Select(b => $"{b:x2}"));
    }

    private static byte[] MultiStringToBytes(string[] values)
    {
        var ms = new MemoryStream();
        foreach (var v in values)
        {
            byte[] b = Encoding.Unicode.GetBytes(v + "\0");
            ms.Write(b, 0, b.Length);
        }
        ms.Write(Encoding.Unicode.GetBytes("\0"), 0, 2);
        return ms.ToArray();
    }

    public static List<RegKeyEntry> ParseRegContent(string regContent)
    {
        var entries = new List<RegKeyEntry>();
        using var reader = new StringReader(regContent);
        string? line;
        RegKeyEntry? currentKey = null;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                string rawKey = line[1..^1];
                bool isDeletion = rawKey.StartsWith('-');
                if (isDeletion) rawKey = rawKey[1..];

                currentKey = new RegKeyEntry(rawKey, new List<RegValueEntry>(), isDeletion);
                entries.Add(currentKey);
                continue;
            }

            if (currentKey == null) continue;

            int eqIndex = line.IndexOf('=');
            if (eqIndex <= 0) continue;

            string namePart = line[..eqIndex].Trim();
            string valuePart = line[(eqIndex + 1)..].Trim();

            string valName = "";
            if (namePart == "@")
            {
                valName = "";
            }
            else if (namePart.StartsWith('"') && namePart.EndsWith('"'))
            {
                valName = namePart[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
            }

            if (valuePart.StartsWith('"') && valuePart.EndsWith('"'))
            {
                string strVal = valuePart[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
                currentKey.Values.Add(new RegValueEntry(valName, strVal, RegistryValueKind.String));
            }
            else if (valuePart.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
            {
                string hex = valuePart[6..];
                uint dwVal = Convert.ToUInt32(hex, 16);
                currentKey.Values.Add(new RegValueEntry(valName, unchecked((int)dwVal), RegistryValueKind.DWord));
            }
            else if (valuePart.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = ParseHexBytes(valuePart[4..], reader);
                currentKey.Values.Add(new RegValueEntry(valName, bytes, RegistryValueKind.Binary));
            }
            else if (valuePart.StartsWith("hex(2):", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = ParseHexBytes(valuePart[7..], reader);
                string str = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                currentKey.Values.Add(new RegValueEntry(valName, str, RegistryValueKind.ExpandString));
            }
            else if (valuePart.StartsWith("hex(7):", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = ParseHexBytes(valuePart[7..], reader);
                string full = Encoding.Unicode.GetString(bytes);
                string[] parts = full.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                currentKey.Values.Add(new RegValueEntry(valName, parts, RegistryValueKind.MultiString));
            }
        }

        return entries;
    }

    private static byte[] ParseHexBytes(string firstLine, StringReader reader)
    {
        var sb = new StringBuilder(firstLine);
        while (sb.ToString().TrimEnd().EndsWith('\\'))
        {
            string trimmed = sb.ToString().TrimEnd();
            sb.Clear();
            sb.Append(trimmed[..^1]);
            string? nextLine = reader.ReadLine();
            if (nextLine == null) break;
            sb.Append(nextLine.Trim());
        }

        string[] tokens = sb.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            bytes[i] = Convert.ToByte(tokens[i].Trim(), 16);
        }
        return bytes;
    }
}
