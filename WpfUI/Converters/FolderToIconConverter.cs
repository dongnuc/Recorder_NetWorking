using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfUI.Converters 
{
    public class FolderToIconConverter : IValueConverter
    {
        private const string FolderIcon = "\uE8B7";
        private const string FileIcon = "\uE7C3";  

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFolder)
            {
                return isFolder ? FolderIcon : FileIcon;
            }
            return FileIcon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}