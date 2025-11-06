using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfUI.ViewModels // <<< Namespace rất quan trọng
{
    public class FileSystemItemViewModel : INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string FullPath { get; set; }
        public bool IsFolder { get; set; }

        public ObservableCollection<FileSystemItemViewModel> Children { get; set; }

        public FileSystemItemViewModel()
        {
            Children = new ObservableCollection<FileSystemItemViewModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}