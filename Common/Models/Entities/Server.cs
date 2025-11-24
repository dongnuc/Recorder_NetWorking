namespace Common.Models.Entities
{
    public class Server
    {
        public int Stage { get; set; }
        public string Console { get; set; } = string.Empty;
        public string Host { get; set; } = "localhost";
        public int Port { get; set; }

        /// <summary>
        /// Get the server's port
        /// </summary>
        /// <returns>The port number the server is listening on</returns>
        public int GetPort()
        {
            return Port;
        }
    }
}