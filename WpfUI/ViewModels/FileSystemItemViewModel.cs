using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfUI.ViewModels
{
    public class FileSystemItemViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
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