namespace Common.Models.Entities
{
    public class OutputServer
    {
        public int Stage { get; set; }
        public string Method { get; set; }
        public string DataRequest { get; set; }
        public string Output { get; set; }
        public string DataTypeMiddleware { get; set; }
        public string ByteSize { get; set; }

    }
}