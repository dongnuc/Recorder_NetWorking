using System.Collections.ObjectModel;

namespace Common.Models.Entities
{
    public class TestStage
    {
        public User? User { get; set; } 
        public Client? Client { get; set; }
        public Server? Server { get; set; } 
        public Database? Database { get; set; } 
        public Network? Network { get; set; } 

    }

}