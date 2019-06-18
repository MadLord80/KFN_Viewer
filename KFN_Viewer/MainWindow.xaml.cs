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
            FilesEncodingElement.DisplayMemberPath = "Value";
            FilesEncodingElement.ItemsSource = encodings;
            FilesEncodingElement.SelectedIndex = 0;
            FilesEncodingElement.IsEnabled = false;

            OpenFileDialog.Filter = "KFN files (*.kfn)|*.kfn|All files (*.*)|*.*";

            viewConfigButton.Click += ViewConfigButtonClick;
            viewConfigButton.IsEnabled = false;
            //createEMZ2Button.IsEnabled = false;
            //createEMZButton.IsEnabled = false;
            ResourceViewInit();
#if !DEBUG
            testButton.Visibility = Visibility.Hidden;               
            ExportAllButton.Visibility = Visibility.Hidden;
#endif
        }

        private void ResourceViewInit()
        {
            GridView resourceGrid = new GridView
            {
                ColumnHeaderContainerStyle =
                    System.Windows.Application.Current.Resources["GridViewColumnHeaderStyle"] as Style
            };
            resourceGrid.Columns.Add(new GridViewColumn()
            {
                Header = "AES Enc",
                Width = 60,
                DisplayMemberBinding = new System.Windows.Data.Binding("IsEncrypted")
            });
            resourceGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Type",
                DisplayMemberBinding = new System.Windows.Data.Binding("FileType")
            });
            resourceGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Name",
                DisplayMemberBinding = new System.Windows.Data.Binding("FileName")
            });
            resourceGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Size",
                DisplayMemberBinding = new System.Windows.Data.Binding("FileSize")
            });

            resourcesView.View = resourceGrid;

            System.Windows.Controls.ContextMenu context = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem exportItem = new System.Windows.Controls.MenuItem() { Header = "Export" };
            exportItem.Click += ExportResourceButtonClick;
            context.Items.Add(exportItem);
            resourcesView.ContextMenu = context;
            resourcesView.ContextMenuOpening += resourcesViewContext;
        }

        private void resourcesViewContext(object sender, ContextMenuEventArgs e)
        {
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;
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

        //private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        private void OpenKFNMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                viewConfigButton.IsEnabled = false;
                //createEMZButton.IsEnabled = false;
                //createEMZ2Button.IsEnabled = false;
                KFN = new KFN(OpenFileDialog.FileName);
                if (KFN.isError != null)
                {
                    System.Windows.MessageBox.Show(KFN.isError);
                    return;
                }
                MainWindowElement.Title = this.windowTitle + " - " + KFN.FileName;
                this.UpdateKFN();
                viewConfigButton.IsEnabled = true;

                KFN.ResorceFile resource = KFN.Resources.Where(r => r.FileName == "Song.ini").First();
                byte[] data = KFN.GetDataFromResource(resource);
                string iniText = new string(Encoding.UTF8.GetChars(data));
                sINI = new SongINI(iniText);
                int textBlocksCount = sINI.Blocks.Where(b => b.Id == "1" || b.Id == "2").ToArray().Length;
                KFN.ResorceFile video = KFN.GetVideoResource();
                if (textBlocksCount == 1)
                {
                    //createEMZButton.IsEnabled = true;
                    //if (video != null) { createEMZ2Button.IsEnabled = true; }
                }
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

            FilesEncodingElement.IsEnabled = true;
        }

        private void FilesEncodingElement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            KeyValuePair<int, string> selectedEncoding = (KeyValuePair<int, string>)FilesEncodingElement.SelectedItem;
            if (KFN != null)
            {
                KFN.ReadFile(selectedEncoding.Key);
                this.UpdateKFN();
            }
        }

        private void createEMZ2Button_Click(object sender, RoutedEventArgs e)
        {
            this.createEMZ(true);
        }

        private void createEMZButton_Click(object sender, RoutedEventArgs e)
        {
            this.createEMZ();
        }

        private void createEMZ(bool withVideo = false)
        {
            SongINI.BlockInfo block = sINI.Blocks.Where(b => b.Id == "1" || b.Id == "2").First();
            byte[] emzData = KFN.createEMZ(block.Content, withVideo);
            if (emzData == null)
            {
                System.Windows.MessageBox.Show((KFN.isError != null)
                    ? KFN.isError
                    : "Fail to create EMZ!");
                return;
            }

            FileInfo kfnFile = new FileInfo(KFN.FileName);
            string emzFileName = kfnFile.Name.Substring(0, kfnFile.Name.Length - kfnFile.Extension.Length) + ".emz";
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

                using (FileStream fs = new FileStream(exportFolder + "\\" + emzFileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(emzData, 0, emzData.Length);
                }
                System.Windows.MessageBox.Show("Export OK: " + exportFolder + "\\" + emzFileName);
            }
        }

        public void ViewConfigButtonClick(object sender, RoutedEventArgs e)
        {
            Window songINI = new SongINIWindow(KFN);
            songINI.Show();
        }

        public void ViewResourceButtonClick(object sender, RoutedEventArgs e)
        {
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;

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
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;

            FolderBrowserDialog.SelectedPath = new FileInfo(KFN.FileName).DirectoryName;
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

        private void ExportResourceToFile(KFN.ResorceFile resource, string folder)
        {
            byte[] data = KFN.GetDataFromResource(resource);

            using (FileStream fs = new FileStream(folder + "\\" + resource.FileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(data, 0, data.Length);
            }
        }
        private void ExportTextToFile(string fileName, byte[] text, string folder)
        {
            using (FileStream fs = new FileStream(folder + "\\" + fileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(text, 0, text.Length);
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

        // karaore text
        //https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/how-to-create-outlined-text
        //https://ru.stackoverflow.com/questions/630777/%D0%97%D0%B0%D0%BA%D1%80%D0%B0%D1%81%D0%B8%D1%82%D1%8C-%D1%82%D0%B5%D0%BA%D1%81%D1%82-%D1%81%D0%BB%D0%BE%D0%B2%D0%BE-%D0%B1%D1%83%D0%BA%D0%B2%D1%83
    }
}
