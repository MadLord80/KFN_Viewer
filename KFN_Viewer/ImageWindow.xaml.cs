using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для ImageWindow.xaml
    /// </summary>
    public partial class ImageWindow : Window
    {
        public ImageWindow(string name, byte[] image)
        {
            InitializeComponent();

            ImageWindowElement.Title += name;
            BitmapImage picture = LoadImage(image);
            ImageElement.Source = picture;
            ImageWindowElement.Title += " (" + picture.Width + "x" + picture.Height + ")";
            ImageWindowElement.Width = picture.PixelWidth;
            ImageWindowElement.Height = picture.PixelHeight;
        }

        private static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
    }
}
