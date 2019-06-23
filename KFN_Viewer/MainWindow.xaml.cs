using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.IO;

using Mozilla.NUniversalCharDet;

namespace KFN_Viewer
{
    public partial class MainWindow : Window
    {
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private readonly FolderBrowserDialog FolderBrowserDialog = new FolderBrowserDialog();
        private string windowTitle = "KFN Viewer";
        private KFN KFN;
        private SongINI sINI;
        private bool needDecryptKFN = true;

        private readonly Dictionary<int, string> encodings = new Dictionary<int, string>
        { { 0, "Use auto detect" } };

        public MainWindow()
        {
            InitializeComponent();

            string version = System.Windows.Forms.Application.ProductVersion;
            this.windowTitle += " v." + version.Remove(version.Length - 2);
            MainWindowElement.Title = this.windowTitle;

            foreach (EncodingInfo enc in Encoding.GetEncodings())
            {
                encodings.Add(enc.CodePage, enc.CodePage + ": " + enc.DisplayName);
            }
            foreach (KeyValuePair<int,string> enc in encodings)
            {
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem {
                    Header = enc.Value, Tag = enc.Key, IsCheckable = true                  
                };
                mi.Click += resEncMenuItem_Select;
                if (enc.Key == 0) { mi.IsChecked = true; }
                resEncMenuItem.Items.Add(mi);
            }
            resEncMenuItem.IsEnabled = false;

            OpenFileDialog.Filter = "KFN files (*.kfn)|*.kfn|All files (*.*)|*.*";

            viewConfigButton.Click += ViewConfigButtonClick;
            viewConfigButton.IsEnabled = false;
            toEMZMenu.IsEnabled = false;
            toMP3LRCMenu.IsEnabled = false;
            toKFNMenu.IsEnabled = false;
            decryptKFN.IsEnabled = false;
            decryptKFN.IsChecked = true;
            ResourceViewInit();
        }

        private void resEncMenuItem_Select(object sender, RoutedEventArgs e)
        {
            int enc = Convert.ToInt32(((System.Windows.Controls.MenuItem)sender).Tag.ToString());
            foreach (var item in resEncMenuItem.Items)
            {
                ((System.Windows.Controls.MenuItem)item).IsChecked = 
                    (((System.Windows.Controls.MenuItem)item).Tag == ((System.Windows.Controls.MenuItem)sender).Tag)
                    ? true : false;
            }
            if (KFN != null)
            {
                KFN.ReadFile(enc);
                this.UpdateKFN();
            }
        }

        private void ResourceViewInit()
        {
            System.Windows.Controls.ContextMenu context = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem exportItem = new System.Windows.Controls.MenuItem() { Header = "Export" };
            exportItem.Click += ExportResourceButtonClick;
            context.Items.Add(exportItem);
            resourcesView.ContextMenu = context;
            resourcesView.ContextMenuOpening += resourcesViewContext;
        }

        private void resourcesViewContext(object sender, ContextMenuEventArgs e)
        {
            KFN.ResourceFile resource = resourcesView.SelectedItem as KFN.ResourceFile;
            System.Windows.Controls.ContextMenu rvcontext = resourcesView.ContextMenu;
            if (rvcontext.Items.Count > 1)
            {
                while (rvcontext.Items.Count > 1)
                {
                    rvcontext.Items.RemoveAt(1);
                }
            }

            if (resource.FileType == "Text" || resource.FileType == "Image")
            {
                System.Windows.Controls.MenuItem viewItem = new System.Windows.Controls.MenuItem() { Header = "View" };
                viewItem.Click += ViewResourceButtonClick;
                rvcontext.Items.Add(viewItem);
            }
        }

        private void OpenKFNMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                viewConfigButton.IsEnabled = false;
                KFN = new KFN(OpenFileDialog.FileName);
                if (KFN.isError != null)
                {
                    System.Windows.MessageBox.Show(KFN.isError);
                    return;
                }
                MainWindowElement.Title = this.windowTitle + " - " + KFN.FullFileName;
                this.UpdateKFN();
                viewConfigButton.IsEnabled = true;
                toEMZMenu.IsEnabled = true;
                toMP3LRCMenu.IsEnabled = true;
                toKFNMenu.IsEnabled = true;

                KFN.ResourceFile encResource = KFN.Resources.Where(r => r.IsEncrypted == true).FirstOrDefault();
                if (encResource != null) { decryptKFN.IsEnabled = true; }
            }
        }

        private void UpdateKFN()
        {
            propertiesView.ItemsSource = KFN.Properties;
            if (KFN.UnknownProperties.Count > 0)
            {
                System.Windows.MessageBox.Show("This KFN file has properties that programm don`t know." +
                    "\nPlease send this file to madlord80@gmail.com for support");
            }
            AutoDetectedEncLabel.Content = KFN.AutoDetectEncoding;

            resourcesView.ItemsSource = KFN.Resources;
            resourcesView.Items.Refresh();
            AutoSizeColumns(resourcesView.View as GridView);

            resEncMenuItem.IsEnabled = true;
        }        

        public void ViewConfigButtonClick(object sender, RoutedEventArgs e)
        {
            Window songINI = new SongINIWindow(KFN);
            songINI.Show();
        }

        public void ViewResourceButtonClick(object sender, RoutedEventArgs e)
        {
            KFN.ResourceFile resource = resourcesView.SelectedItem as KFN.ResourceFile;

            if (resource.FileType == "Text")
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

                string text = new string(Encoding.GetEncoding(detEncoding).GetChars(data));
                Window viewWindow = new ViewWindow(
                    resource.FileName,
                    text,
                    Encoding.GetEncodings().Where(en => en.CodePage == detEncoding).First().DisplayName
                );
                viewWindow.Show();
            }            
            else if (resource.FileType == "Image")
            {
                byte[] data = KFN.GetDataFromResource(resource);

                Window viewWindow = new ImageWindow(resource.FileName, data);
                viewWindow.Show();
            }
        }

        private void ExportResourceButtonClick(object sender, RoutedEventArgs e)
        {
            KFN.ResourceFile resource = resourcesView.SelectedItem as KFN.ResourceFile;

            FolderBrowserDialog.SelectedPath = new FileInfo(KFN.FullFileName).DirectoryName;
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

                ExportResourceToFile(resource, exportFolder);
                System.Windows.MessageBox.Show("Export OK: " + exportFolder + "\\" + resource.FileName);
            }
        }

        private void ExportResourceToFile(KFN.ResourceFile resource, string folder)
        {
            byte[] data = KFN.GetDataFromResource(resource);

            using (FileStream fs = new FileStream(folder + "\\" + resource.FileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(data, 0, data.Length);
            }
        }

        private void AutoSizeColumns(GridView gv)
        {
            if (gv != null)
            {
                foreach (var c in gv.Columns)
                {
                    // Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
                    // i.e. it is the almost same code that is executed when the gripper is double clicked
                    if (double.IsNaN(c.Width))
                    {
                        c.Width = c.ActualWidth;
                    }
                    else
                    {
                        continue;
                    }
                    c.Width = double.NaN;
                }
            }
        }

        // TODO (maybe)
        private void Test(object sender, RoutedEventArgs e)
        {
            //Window playerWindow = new PlayWindow("D:\\DJ Piligrim LIVE @ Disco MCLUB (Augsburg) - 20. Mai 2009.avi");
            //playerWindow.Show();
        }

        private void toEMZMenu_Click(object sender, RoutedEventArgs e)
        {
            ExportWindow exportWindow = new ExportWindow("EMZ", this.KFN);
            exportWindow.Show();
        }

        private void ToMP3LRCMenu_Click(object sender, RoutedEventArgs e)
        {
            ExportWindow exportWindow = new ExportWindow("MP3+LRC", this.KFN);
            exportWindow.Show();
        }

        private void ToKFNMenu_Click(object sender, RoutedEventArgs e)
        {
            List<KFN.ResourceFile> rs = new List<KFN.ResourceFile>();
            string audioSource = KFN.GetAudioSourceName();
            KFN.ResourceFile audio = KFN.Resources.Where(r => r.FileName == audioSource).First();
            KFN.ResourceFile config = KFN.Resources.Where(r => r.FileName == "Song.ini").First();
            rs.Add(audio);
            rs.Add(config);
            KFN.ChangeKFN(rs);
            //KFN.DecryptKFN();
            //if (KFN.isError != null)
            //{
            //    System.Windows.MessageBox.Show(KFN.isError);
            //    return;
            //}
            System.Windows.MessageBox.Show("Done!");
            KFN = new KFN(KFN.FullFileName);
            if (KFN.isError != null)
            {
                System.Windows.MessageBox.Show(KFN.isError);
                return;
            }
            this.UpdateKFN();

            //just decrypted
            //only with audio and lyric(and modified ? Song.ini, +-decrypt)
            //select lyric
        }

        private void SelectAllResources_Click(object sender, RoutedEventArgs e)
        {
            KFN.Resources.ForEach(r => r.IsExported = (bool)selectAllResources.IsChecked);
            resourcesView.Items.Refresh();
        }

        private void DecryptKFN_Click(object sender, RoutedEventArgs e)
        {
            this.needDecryptKFN = (bool)decryptKFN.IsChecked;
        }

        // karaore text
        //https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/how-to-create-outlined-text
        //https://ru.stackoverflow.com/questions/630777/%D0%97%D0%B0%D0%BA%D1%80%D0%B0%D1%81%D0%B8%D1%82%D1%8C-%D1%82%D0%B5%D0%BA%D1%81%D1%82-%D1%81%D0%BB%D0%BE%D0%B2%D0%BE-%D0%B1%D1%83%D0%BA%D0%B2%D1%83
    }
}
