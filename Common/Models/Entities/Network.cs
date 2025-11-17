namespace Common.Models.Entities
{
    public class Network
    {
        public int Stage { get; set; }
        public string Url { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty ;
        public string REQ_Payload { get; set; } = string.Empty;
        public string RES_Payload { get; set; } = string.Empty;
    }
}
