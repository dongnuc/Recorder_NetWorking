namespace Common.Models.Entities
{
    public class OutputClient
    {
        public int Stage { get; set; }
        public string Method { get; set; }
        public string DataResponse { get; set; }

        public string StatusCode { get; set; }
        public string Output { get; set; }
        public string DataTypeMiddleWare { get; set; }
        public string ByteSize { get; set; }
    }
}