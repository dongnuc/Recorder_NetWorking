using Common.Logging;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace Common.Helper
{
    public class DataInspector
    {
        public static string DetecDataType(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "Empty";

            // 1️⃣ Kiểm tra magic bytes (file định dạng)
            string fileType = DetectFileType(data);
            if (fileType != null)
                return fileType; // nếu phát hiện là file ảnh/pdf/zip,...

            // 2️⃣ Kiểm tra xem có quá nhiều byte không in được => Binary
            if (IsBinaryData(data))
                return "Binary";

            // 3️⃣ Chuyển qua text để kiểm tra các dạng Text-based
            string asString = Encoding.UTF8.GetString(data)
                .Trim('\uFEFF', '\u200B', '\u0000')
                .Trim();

            // 4️⃣ Kiểm tra số
            if (long.TryParse(asString, out _))
                return "Integer";

            // 5️⃣ Kiểm tra JSON
            if ((asString.StartsWith("{") && asString.EndsWith("}")) ||
                (asString.StartsWith("[") && asString.EndsWith("]")))
            {
                try
                {
                    using var doc = JsonDocument.Parse(asString);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object ||
                        root.ValueKind == JsonValueKind.Array)
                        return "JSON";
                }
                catch { }
            }

            // 6️⃣ Kiểm tra XML
            if (asString.StartsWith("<") && asString.EndsWith(">"))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(asString);
                    return "XML";
                }
                catch { }
            }

            // 7️⃣ Kiểm tra chuỗi printable
            if (IsPrintableString(data))
                return "String";

            // 8️⃣ Mặc định
            return "Binary";
        }

        private static bool IsBinaryData(byte[] data)
        {
            // Nếu nhiều byte không thuộc printable ASCII => Binary
            int nonPrintableCount = data.Count(b => (b < 32 && b != 9 && b != 10 && b != 13) || b > 126);
            return ((double)nonPrintableCount / data.Length) > 0.2;
        }

        private static bool IsPrintableString(byte[] data)
        {
            foreach (byte b in data)
            {
                if (b < 32 && b != 9 && b != 10 && b != 13)
                    return false;
            }
            return true;
        }

        private static string DetectFileType(byte[] data)
        {
            if (StartsWith(data, 0xFF, 0xD8, 0xFF)) return "Image(JPEG)";
            if (StartsWith(data, 0x89, 0x50, 0x4E, 0x47)) return "Image(PNG)";
            if (StartsWith(data, 0x47, 0x49, 0x46, 0x38)) return "Image(GIF)";
            if (StartsWith(data, 0x42, 0x4D)) return "Image(BMP)";
            if (StartsWith(data, 0x25, 0x50, 0x44, 0x46)) return "File(PDF)";
            if (StartsWith(data, 0x50, 0x4B, 0x03, 0x04)) return "File(ZIP/DOCX)";
            return null;
        }

        private static bool StartsWith(byte[] data, params byte[] prefix)
        {
            if (data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[i] != prefix[i]) return false;
            return true;
        }

        public static string ExtractDifference(string before, string after, string stageName = null)
        {
            if (!string.IsNullOrEmpty(stageName))
            {
                LogManager.Instance.LogDebug($"🔍 Extracting difference for: {stageName}");
            }

            // Handle empty cases
            if (string.IsNullOrEmpty(after))
            {
                LogManager.Instance.LogDebug("   ⚠️ AFTER is empty - No new content");
                return string.Empty;
            }

            if (string.IsNullOrEmpty(before))
            {
                LogManager.Instance.LogDebug($"   ✅ BEFORE is empty - Return full AFTER ({after.Length} chars)");
                LogManager.Instance.LogInfomation(after);
                return after;
            }

            string beforeTrimmed = before;
            string afterTrimmed = after;

            // Check if AFTER starts with BEFORE
            if (afterTrimmed.Length > beforeTrimmed.Length &&
                afterTrimmed.StartsWith(beforeTrimmed, StringComparison.Ordinal))
            {
                // Extract new content
                string newContent = afterTrimmed.Substring(beforeTrimmed.Length);

                LogManager.Instance.LogDebug($"   ✅ Extracted {newContent.Length} new characters");
                LogManager.Instance.LogInfomation(newContent);

                return newContent;
            }
            else if (!string.Equals(afterTrimmed, beforeTrimmed, StringComparison.Ordinal))
            {
                // AFTER is completely different
                LogManager.Instance.LogDebug($"   ⚠️ AFTER is different (not a continuation) - Return full AFTER");
                LogManager.Instance.LogInfomation(afterTrimmed);

                return afterTrimmed;
            }
            else
            {
                // No change
                LogManager.Instance.LogDebug("   ℹ️ No changes detected");
                return string.Empty;
            }
        }

        /// <summary>
        /// ✅ IMPROVED: Extract input với better logging
        /// </summary>
        public static string ExtractInputFromLastLine(string before, string after)
        {
            LogManager.Instance.LogDebug("🔍 ===== ExtractInputFromLastLine START =====");
            LogManager.Instance.LogDebug($"BEFORE length: {before?.Length ?? 0}");
            LogManager.Instance.LogDebug($"AFTER length: {after?.Length ?? 0}");

            string afterTrimmed = (after ?? string.Empty).TrimEnd();
            if (afterTrimmed.Length == 0)
            {
                LogManager.Instance.LogDebug("⚠️ AFTER is empty");
                return string.Empty;
            }

            string beforeTrimmed = (before ?? string.Empty).TrimEnd();

            // ✅ Log raw content
            LogManager.Instance.LogDebug($"BEFORE (last 100 chars): {(beforeTrimmed.Length > 100 ? beforeTrimmed.Substring(beforeTrimmed.Length - 100) : beforeTrimmed)}");
            LogManager.Instance.LogDebug($"AFTER (last 100 chars): {(afterTrimmed.Length > 100 ? afterTrimmed.Substring(afterTrimmed.Length - 100) : afterTrimmed)}");

            // Case 1: BEFORE is empty
            if (beforeTrimmed.Length == 0)
            {
                var lines = afterTrimmed.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    string lastLine = lines[lines.Length - 1].Trim();
                    LogManager.Instance.LogDebug($"Case 1: Last line from AFTER: [{lastLine}]");

                    // ✅ Extract after colon
                    int colon = lastLine.LastIndexOf(':');
                    if (colon >= 0 && colon + 1 < lastLine.Length)
                    {
                        string input = lastLine.Substring(colon + 1).Trim();
                        LogManager.Instance.LogDebug($"✅ Extracted (after colon): [{input}]");
                        return input;
                    }

                    // ✅ Return full last line if no colon
                    LogManager.Instance.LogDebug($"✅ Extracted (full line): [{lastLine}]");
                    return lastLine;
                }

                return string.Empty;
            }

            // Case 2: AFTER starts with BEFORE
            if (afterTrimmed.Length >= beforeTrimmed.Length &&
                afterTrimmed.StartsWith(beforeTrimmed, StringComparison.Ordinal))
            {
                string diff = afterTrimmed.Substring(beforeTrimmed.Length);
                LogManager.Instance.LogDebug($"Case 2: Difference: [{diff}]");

                // ✅ Split difference into lines
                var diffLines = diff.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

                if (diffLines.Length > 0)
                {
                    // ✅ Get FIRST non-empty line (this is user input)
                    string firstLine = diffLines[0].Trim();
                    LogManager.Instance.LogDebug($"✅ Extracted (first line of diff): [{firstLine}]");
                    return firstLine;
                }

                // ✅ If diff has no newline, return trimmed diff
                string trimmedDiff = diff.Trim();
                LogManager.Instance.LogDebug($"✅ Extracted (trimmed diff): [{trimmedDiff}]");
                return trimmedDiff;
            }

            // Case 3: Completely different
            var afterLines = afterTrimmed.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            if (afterLines.Length > 0)
            {
                string lastLine = afterLines[afterLines.Length - 1].Trim();
                LogManager.Instance.LogDebug($"Case 3: Last line from AFTER: [{lastLine}]");

                int colon = lastLine.LastIndexOf(':');
                if (colon >= 0 && colon + 1 < lastLine.Length)
                {
                    string input = lastLine.Substring(colon + 1).Trim();
                    LogManager.Instance.LogDebug($"✅ Extracted (after colon): [{input}]");
                    return input;
                }

                LogManager.Instance.LogDebug($"✅ Extracted (last line): [{lastLine}]");
                return lastLine;
            }

            LogManager.Instance.LogDebug("⚠️ Could not extract input");
            LogManager.Instance.LogDebug("🔍 ===== ExtractInputFromLastLine END =====");
            return string.Empty;
        }

        public static (string input, string output) SplitInputFromOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return (string.Empty, string.Empty);
            }

            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            if (lines.Length == 0)
            {
                return (string.Empty, string.Empty);
            }

            // Case 1: Chỉ có 1 dòng → Coi là input
            if (lines.Length == 1)
            {
                return (lines[0].Trim(), string.Empty);
            }

            // Case 2: Dòng đầu tiên là input, còn lại là output
            string firstLine = string.Empty;
            if (!string.IsNullOrEmpty(lines[0]))
            {
                firstLine = lines[0];
            }

            // Lấy tất cả dòng từ dòng 2 trở đi
            var outputLines = lines.Skip(1);
            string output = string.Join(Environment.NewLine, outputLines);

            return (firstLine, output);
        }
    }
}
