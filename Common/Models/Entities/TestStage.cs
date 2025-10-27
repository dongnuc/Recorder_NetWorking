using System.Collections.ObjectModel;

namespace Common.Models.Entities
{
    public class TestStage
    {
        public ObservableCollection<InputClient> InputClients { get; set; } = new();
        public ObservableCollection<OutputClient> OutputClients { get; set; } = new();
        public ObservableCollection<OutputServer> OutputServers { get; set; } = new();
    }

}