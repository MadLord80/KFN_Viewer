using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для PlayWindow.xaml
    /// </summary>
    public partial class PlayWindow : Window
    {
        //private string mediaFile;

        public PlayWindow(string mediaFile)
        {
            InitializeComponent();

            PlayFile(mediaFile);
        }

        private void PlayFile(string file)
        {
            VideoElement.Source = new Uri(file);
            LyricsElement.Text = "TEST text";
            VideoElement.Play();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            VideoElement.Stop();
        }
    }
}
