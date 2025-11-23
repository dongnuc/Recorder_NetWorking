namespace Common.Models.Entities
{
    public enum ConnectionState
    {
        New,                // Mới khởi tạo
        ConnectingToServer, // Đang thử kết nối Server
        Established,        // Đã kết nối 2 đầu
        Failed,             // Lỗi kết nối
        Closing,            // Đang đóng
        ClosedByClient,     // Client ngắt trước
        ClosedByServer,     // Server ngắt trước
        ClosedWithError     // Đóng do lỗi
    }

    public class ConnectionTrace
    {
        public int Stage { get; set; }
        public ConnectionState State { get; set; } = ConnectionState.New;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Note { get; set; } = string.Empty;
    }
}
