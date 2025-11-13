using DatabaseServices.Services;
using Common.Interfaces.Services;

namespace DatabaseServices.Examples
{
    /// <summary>
    /// Example demonstrating how to use the ResetDatabaseService
    /// </summary>
    public class ResetDatabaseExample
    {
        public static async Task Main(string[] args)
        {
            // ========================================
            // Ví dụ sử dụng ResetDatabaseService
            // ========================================

            // Bước 1: Chuẩn bị connection string
            // Thay đổi connection string theo database của bạn
            string connectionString = "Server=localhost;Database=master;Integrated Security=true;";
            
            // Hoặc dùng SQL Server Authentication:
            // string connectionString = "Server=localhost;Database=master;User Id=sa;Password=YourPassword;";

            // Bước 2: Đường dẫn đến file SQL script
            string sqlScriptPath = @"D:\CapstoneProject\middlewareTool\PRN222-Project-sample-pe-sp25\PE_PRN222_GivenSolution1\PE_PRN222_sp25.sql";

            // Bước 3: Khởi tạo service
            IDatabaseServices resetService = new ResetDatabaseService(connectionString);

            try
            {
                Console.WriteLine("=== Database Reset Service Test ===");
                Console.WriteLine($"SQL Script: {sqlScriptPath}");
                Console.WriteLine($"Connection: {connectionString.Replace(connectionString.Split(';').LastOrDefault(s => s.Contains("Password")) ?? "", "Password=***")}");
                Console.WriteLine();
                Console.WriteLine("Đang thực thi script SQL...");

                // Bước 4: Thực thi reset database
                await resetService.ResetDatabaseAsync(sqlScriptPath);

                Console.WriteLine();
                Console.WriteLine("✅ Thành công! Database đã được reset.");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"❌ Lỗi: Không tìm thấy file SQL - {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"❌ Lỗi: Tham số không hợp lệ - {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"❌ Lỗi: File SQL rỗng - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi thực thi: {ex.Message}");
                Console.WriteLine($"Chi tiết: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("Nhấn phím bất kỳ để thoát...");
            Console.ReadKey();
        }
    }
}
