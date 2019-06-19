using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для ExportWindow.xaml
    /// </summary>
    public partial class ExportWindow : Window
    {
        private KFN KFN;

        public ExportWindow(string exportType, KFN KFN)
        {
            InitializeComponent();

            WindowElement.Title += " " + exportType;
            this.KFN = KFN;

            videoLabel.Visibility = (exportType == "EMZ") ? Visibility.Visible : Visibility.Hidden;
            videoSelect.Visibility = (exportType == "EMZ") ? Visibility.Visible : Visibility.Hidden;
            deleteID3Tags.Visibility = (exportType == "MP3+LRC") ? Visibility.Visible : Visibility.Hidden;

            if (exportType == "EMZ")
            {
                List<KFN.ResorceFile> videos = KFN.Resources.Where(r => r.FileType == "Video").ToList();
                videos.Add(new KFN.ResorceFile("Video", "don`t use video", 0, 0, 0, false));
                videoSelect.ItemsSource = videos;
                videoSelect.DisplayMemberPath = "FileName";
                videoSelect.SelectedIndex = 0;
            }
        }
    }
}
