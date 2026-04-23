using System.Text;

namespace YogitaFashionAPI.Services
{
    public static class CsvExportService
    {
        public static byte[] BuildCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", headers.Select(Escape)));
            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(",", row.Select(Escape)));
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static string Escape(string? value)
        {
            var safe = value ?? "";
            var containsSpecial = safe.Contains(',') || safe.Contains('"') || safe.Contains('\n') || safe.Contains('\r');
            if (!containsSpecial)
            {
                return safe;
            }

            return $"\"{safe.Replace("\"", "\"\"")}\"";
        }
    }
}
