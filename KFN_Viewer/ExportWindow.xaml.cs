using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

using Mozilla.NUniversalCharDet;

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
            playVideoButton.Visibility = (exportType == "EMZ") ? Visibility.Visible : Visibility.Hidden;
            deleteID3Tags.IsChecked = true;
            deleteID3Tags.Visibility = (exportType == "MP3+LRC") ? Visibility.Visible : Visibility.Hidden;
            artistLabel.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            titleLabel.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            artistTextBox.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            titleTextBox.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;

            // TODO
            playVideoButton.IsEnabled = false;
            playAudioButton.IsEnabled = false;

            // AUDIO
            List<KFN.ResourceFile> audios = KFN.Resources.Where(r => r.FileType == "Audio").ToList();
            string audioSource = KFN.GetAudioSourceName();
            audioSelect.ItemsSource = audios;
            audioSelect.DisplayMemberPath = "FileName";
            audioSelect.SelectedItem = audios.Where(a => a.FileName == audioSource).First();
            if (audios.Count == 1) { audioSelect.IsEnabled = false; }

            // LYRICS
            Dictionary<string, string> lyrics = new Dictionary<string, string>();
            List<KFN.ResourceFile> texts = KFN.Resources.Where(r => r.FileType == "Text").ToList();
            foreach (KFN.ResourceFile resource in texts)
            {
                lyrics.Add(resource.FileName, this.GetResourceText(resource));
            }

            KFN.ResourceFile songIni = KFN.Resources.Where(r => r.FileName == "Song.ini").First();
            byte[] data = KFN.GetDataFromResource(songIni);
            string iniText = new string(Encoding.UTF8.GetChars(data));
            SongINI sINI = new SongINI(iniText);
            foreach (SongINI.BlockInfo block in sINI.Blocks.Where(b => b.Id == "1" || b.Id == "2"))
            {
                string lyricFromBlock = (exportType == "EMZ")
                    ? KFN.INIToELYR(block.Content)
                    : KFN.INIToExtLRC(block.Content);
                if (lyricFromBlock != null)
                {
                    lyrics.Add("Song.ini: " + block.Name, lyricFromBlock);
                }
                else
                {
                    lyrics.Add("Song.ini: " + block.Name, "Can`t convert lyric from Song.ini");
                }
            }
            lyricSelect.DisplayMemberPath = "Key";
            lyricSelect.SelectedIndex = 0;
            if (lyrics.Count == 1) { lyricSelect.IsEnabled = false; }
            lyricSelect.ItemsSource = lyrics;
            lyricPreview.Text = ((KeyValuePair<string, string>)lyricSelect.SelectedItem).Value;

            // ARTIST-TITLE

            // VIDEO
            if (exportType == "EMZ")
            {
                List<KFN.ResourceFile> videos = KFN.Resources.Where(r => r.FileType == "Video").ToList();
                if (videos.Count == 0)
                {
                    videos.Add(new KFN.ResourceFile("Video", "video not found", 0, 0, 0, false));
                    videoSelect.IsEnabled = false;
                }
                else
                {
                    videos.Add(new KFN.ResourceFile("Video", "don`t use video", 0, 0, 0, false));
                }                
                videoSelect.ItemsSource = videos;
                videoSelect.DisplayMemberPath = "FileName";
                videoSelect.SelectedIndex = 0;
            }
        }

        private string GetResourceText(KFN.ResourceFile resource)
        {
            byte[] data = KFN.GetDataFromResource(resource);

            ////UTF-8
            int detEncoding = 65001;
            UniversalDetector Det = new UniversalDetector(null);
            Det.HandleData(data, 0, data.Length);
            Det.DataEnd();
            string enc = Det.GetDetectedCharset();
            if (enc != null && enc != "Not supported")
            {
                // fix encoding for 1251 upper case and MAC
                //if (enc == "KOI8-R" || enc == "X-MAC-CYRILLIC") { enc = "WINDOWS-1251"; }
                Encoding denc = Encoding.GetEncoding(enc);
                detEncoding = denc.CodePage;
            }

            return new string(Encoding.GetEncoding(detEncoding).GetChars(data));
        }

        private void AudioSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            
        }

        private void LyricSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            lyricPreview.Text = ((KeyValuePair<string, string>)lyricSelect.SelectedItem).Value;
        }
    }
}
