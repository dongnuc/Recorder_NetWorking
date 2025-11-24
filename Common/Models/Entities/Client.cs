namespace Common.Models.Entities
{
    public class Client
    {
        public int Stage { get; set; }
        public string Console { get; set; } = string.Empty;
        public string ServerHost { get; set; } = "localhost";
        public int ServerPort { get; set; }
    }
}