using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;

using Mozilla.NUniversalCharDet;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для ExportWindow.xaml
    /// </summary>
    public partial class ExportWindow : Window
    {
        private KFN KFN;
        private string exportType;
        private ID3Tags ID3Class = new ID3Tags();
        private readonly FolderBrowserDialog FolderBrowserDialog = new FolderBrowserDialog();
        private Dictionary<int, string> encodings = new Dictionary<int, string>
        {
            { 0, "ANSI (System default)" },
            { 65001, "UTF-8 (KFN default)" }
        };

        public ExportWindow(string exportType, KFN KFN)
        {
            InitializeComponent();

            WindowElement.Title += exportType;
            this.KFN = KFN;
            this.exportType = exportType;

            videoLabel.Visibility = (exportType == "EMZ") ? Visibility.Visible : Visibility.Hidden;
            videoSelect.Visibility = (exportType == "EMZ") ? Visibility.Visible : Visibility.Hidden;
            playVideoButton.Visibility = (exportType == "EMZ") ? Visibility.Visible : Visibility.Hidden;
            deleteID3Tags.IsChecked = true;
            deleteID3Tags.Visibility = (exportType == "MP3+LRC") ? Visibility.Visible : Visibility.Hidden;
            artistLabel.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            titleLabel.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            artistSelect.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            titleSelect.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            encLabel.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;
            encSelect.Visibility = (exportType != "EMZ") ? Visibility.Visible : Visibility.Hidden;

            // TODO
            playVideoButton.IsEnabled = false;
            playAudioButton.IsEnabled = false;

            // AUDIO
            List<KFN.ResourceFile> audios = KFN.Resources.Where(r => r.FileType == "Audio").ToList();
            string audioSource = KFN.GetAudioSourceName();
            audioSelect.ItemsSource = audios;
            audioSelect.DisplayMemberPath = "FileName";
            audioSelect.SelectedItem = audios.Where(a => a.FileName == audioSource).FirstOrDefault();
            if (audioSelect.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("Can`t find audio source!");
                return;
            }
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
            if (exportType == "MP3+LRC")
            {
                List<string> artists = new List<string> { null };
                List<string> titles = new List<string> { null };

                KeyValuePair<string, string> kfnArtist = KFN.Properties.Where(p => p.Key == "Artist").FirstOrDefault();
                if (kfnArtist.Value != null && kfnArtist.Value.Length > 0) { artists.Add(kfnArtist.Value); }
                KeyValuePair<string, string> kfnTitle = KFN.Properties.Where(p => p.Key == "Title").FirstOrDefault();
                if (kfnTitle.Value != null && kfnTitle.Value.Length > 0) { titles.Add(kfnTitle.Value); }

                foreach (KFN.ResourceFile resource in KFN.Resources.Where(r => r.FileType == "Audio"))
                {
                    string[] atFromID3 = ID3Class.GetArtistAndTitle(KFN.GetDataFromResource(resource));
                    if (atFromID3[0] != null) { artists.Add(atFromID3[0]); }
                    if (atFromID3[1] != null) { titles.Add(atFromID3[1]); }
                }
                artists = artists.Distinct().ToList();
                titles = titles.Distinct().ToList();

                artistSelect.ItemsSource = artists;
                artistSelect.SelectedIndex = 0;
                titleSelect.ItemsSource = titles;
                titleSelect.SelectedIndex = 0;

                encSelect.ItemsSource = this.encodings;
                encSelect.DisplayMemberPath = "Value";
                encSelect.SelectedIndex = 0;
            }

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

        private void UpdateArtistTitleInLRC(string artist, string title)
        {
            string origText = lyricPreview.Text;
            if (artist != null && artist.Length > 0)
            {
                if (Regex.IsMatch(origText, @"\[ar:[^\]]+\]"))
                {
                    origText = Regex.Replace(origText, @"\[ar:[^\n]+", "[ar:" + artist + "]");
                }
                else
                {
                    origText = "[ar:" + artist + "]\n" + origText;
                }
            }
            if (title != null && title.Length > 0)
            {
                if (Regex.IsMatch(origText, @"\[ti:[^\]]+\]"))
                {
                    origText = Regex.Replace(origText, @"\[ti:[^\n]+", "[ti:" + title + "]");
                }
                else
                {
                    origText = "[ti:" + title + "]\n" + origText;
                }
            }
            lyricPreview.Text = origText;
        }

        private void AudioSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            
        }

        private void LyricSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            lyricPreview.Text = ((KeyValuePair<string, string>)lyricSelect.SelectedItem).Value;
        }

        private void ArtistSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.UpdateArtistTitleInLRC((string)artistSelect.SelectedItem, null);
        }

        private void TitleSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.UpdateArtistTitleInLRC(null, (string)titleSelect.SelectedItem);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            KFN.ResourceFile audio = (KFN.ResourceFile)audioSelect.SelectedItem;
            if (audio == null) { return; }

            string lyric = lyricPreview.Text;
            if (lyric.Length == 0 || lyric.Contains("Can`t convert lyric from Song.ini")) { return; }
            
            FileInfo kfnFile = new FileInfo(KFN.FullFileName);
            FolderBrowserDialog.SelectedPath = kfnFile.DirectoryName;
            if (FolderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string exportFolder = FolderBrowserDialog.SelectedPath;
                try
                {
                    System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(exportFolder);
                }
                catch (UnauthorizedAccessException error)
                {
                    System.Windows.MessageBox.Show(error.Message);
                    return;
                }

                if (this.exportType == "EMZ")
                {
                    KFN.ResourceFile video = (KFN.ResourceFile)videoSelect.SelectedItem;
                    byte[] fileData = KFN.createEMZ(lyric, video.FileLength > 0, video, audio);
                    if (fileData == null)
                    {
                        System.Windows.MessageBox.Show((KFN.isError != null)
                            ? KFN.isError
                            : "Fail to create EMZ!");
                        return;
                    }
                    string emzFileName = kfnFile.Name.Substring(0, kfnFile.Name.Length - kfnFile.Extension.Length) + ".emz";
                    using (FileStream fs = new FileStream(exportFolder + "\\" + emzFileName, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(fileData, 0, fileData.Length);
                    }
                    System.Windows.MessageBox.Show("Export OK: " + exportFolder + "\\" + emzFileName);
                }
                else if (this.exportType == "MP3+LRC")
                {
                    FileInfo audioFile = new FileInfo(audio.FileName);
                    string mp3FileName = kfnFile.Name.Substring(0, kfnFile.Name.Length - kfnFile.Extension.Length) + audioFile.Extension;
                    string lrcFileName = kfnFile.Name.Substring(0, kfnFile.Name.Length - kfnFile.Extension.Length) + ".lrc";

                    byte[] mp3Data = KFN.GetDataFromResource(audio);
                    if (deleteID3Tags.IsChecked == true)
                    {
                        mp3Data = ID3Class.RemoveAllTags(mp3Data);
                    }
                    using (FileStream fs = new FileStream(exportFolder + "\\" + mp3FileName, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(mp3Data, 0, mp3Data.Length);
                    }

                    int encCode = ((KeyValuePair<int, string>)encSelect.SelectedItem).Key;
                    Encoding lrcEnc = (encCode == 0) ? Encoding.Default : Encoding.GetEncoding(encCode);
                    byte[] lrcData = lrcEnc.GetBytes(lyric);
                    using (FileStream fs = new FileStream(exportFolder + "\\" + lrcFileName, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(lrcData, 0, lrcData.Length);
                    }
                    System.Windows.MessageBox.Show("Export OK: " + exportFolder + "\\" + mp3FileName);
                }
            }
        }
    }
}
