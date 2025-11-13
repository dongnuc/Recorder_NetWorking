namespace Common.Models.Entities
{
    public class Database
    {
        public int Stage { get; set; }
        public string Query { get; set; } = string.Empty;
        public string ExpectedData { get; set; } = string.Empty;
    }
}
