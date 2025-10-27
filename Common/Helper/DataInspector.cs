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
    }
}
