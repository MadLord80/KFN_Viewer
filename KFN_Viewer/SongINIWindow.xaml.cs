using System;
using System.Windows;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using IniParser.Model;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для SongINIWindow.xaml
    /// </summary>
    public partial class SongINIWindow : Window
    {
        private KFN KFN;
        private readonly FolderBrowserDialog FolderBrowserDialog = new FolderBrowserDialog();

        public SongINIWindow(KFN KFN)
        {
            InitializeComponent();

            this.KFN = KFN;

            GridView blocksGrid = new GridView
            {
                ColumnHeaderContainerStyle =
                    System.Windows.Application.Current.Resources["GridViewColumnHeaderStyle"] as Style
            };
            blocksGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Name",
                Width = 80,
                DisplayMemberBinding = new System.Windows.Data.Binding("Name")
            });
            blocksGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Content ID",
                Width = 80,
                DisplayMemberBinding = new System.Windows.Data.Binding("Id")
            });
            blocksGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Content type",
                DisplayMemberBinding = new System.Windows.Data.Binding("Type")
            });
            iniBlocksView.View = blocksGrid;

            toLRCButton.IsEnabled = false;
            toELYRButton.IsEnabled = false;
            createEMZButton.IsEnabled = false;

            this.ParseINI(KFN);
        }

        private class BlockInfo
        {
            private string name;
            private string id;
            private string type;
            private string content;

            public string Name { get { return this.name; } }
            public string Id { get { return this.id; } }
            public string Type { get { return this.type; } }
            public string Content { get { return this.content; } }

            public BlockInfo(SectionData block, string KFNBlockType)
            {
                this.name = block.SectionName;
                this.id = block.Keys["ID"];
                this.type = KFNBlockType;

                string blockContent = "";
                foreach (KeyData key in block.Keys)
                {
                    blockContent += key.KeyName + "=" + key.Value + "\n";
                }
                this.content = blockContent;
            }
        }

        private void ParseINI(KFN KFN)
        {
            var parser = new IniParser.Parser.IniDataParser();
            KFN.ResorceFile resource = KFN.Resources.Where(r => r.FileName == "Song.ini").First();
            byte[] data = KFN.GetDataFromResource(resource);
            // skip null at the end
            data = data.Reverse().SkipWhile(d => d == 0).ToArray().Reverse().ToArray();
            string iniText = new string(Encoding.UTF8.GetChars(data));

            IniData iniData = parser.Parse(iniText);

            List<BlockInfo> blocksData = new List<BlockInfo>();
            foreach (SectionData block in iniData.Sections)
            {
                string blockId = block.Keys["ID"];
                blocksData.Add(new BlockInfo(
                    block,
                    (blockId != null) ? KFN.GetIniBlockType(Convert.ToInt32(blockId)) : ""
                ));
            }

            iniBlocksView.ItemsSource = blocksData;
            this.AutoSizeColumns(iniBlocksView.View as GridView);
        }

        private void IniBlocksView_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BlockInfo block = iniBlocksView.SelectedItem as BlockInfo;
            blockContent.Text = block.Content;
            toLRCButton.IsEnabled = (block.Id == "1" || block.Id == "2") ? true : false;
            toELYRButton.IsEnabled = (block.Id == "1" || block.Id == "2") ? true : false;
            createEMZButton.IsEnabled = (block.Id == "1" || block.Id == "2") ? true : false;
        }

        private void createEMZButton_Click(object sender, RoutedEventArgs e)
        {
            string audioFile = this.KFN.GetAudioSource();
            if (audioFile == null)
            {
                System.Windows.MessageBox.Show("Can`t find audio source property!");
                return;
            }
            KFN.ResorceFile audioResource = this.KFN.Resources.Where(r => r.FileName == audioFile).FirstOrDefault();
            if (audioResource == null)
            {
                System.Windows.MessageBox.Show("Can`t find resource for audio source property!");
                return;
            }

            KFN.ResorceFile lyricResource = this.KFN.Resources.Where(r => r.FileName == "Song.ini").FirstOrDefault();
            if (lyricResource == null)
            {
                System.Windows.MessageBox.Show("Can`t find Song.ini!");
                return;
            }

            FileInfo sourceFile = new FileInfo(audioFile);
            string elyrFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".elyr";

            BlockInfo block = iniBlocksView.SelectedItem as BlockInfo;
            string elyrText = this.KFN.INIToELYR(block.Content);
            if (elyrText == null)
            {
                System.Windows.MessageBox.Show((KFN.isError != null)
                    ? KFN.isError
                    : "Fail to create ELYR!");
                return;
            }

            byte[] bom = Encoding.Unicode.GetPreamble();
            string elyrHeader = "encore.lg-karaoke.ru ver=02 crc=00000000 \r\n";
            byte[] elyr = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, Encoding.UTF8.GetBytes(elyrHeader + elyrText));
            Array.Resize(ref bom, bom.Length + elyr.Length);
            Array.Copy(elyr, 0, bom, 2, elyr.Length);

            using (MemoryStream memStream = new MemoryStream())
            {
                //int cp = System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
                int cp = 866;
                using (ZipArchive archive = new ZipArchive(memStream, ZipArchiveMode.Create, true, Encoding.GetEncoding(cp)))
                {
                    ZipArchiveEntry lyricEntry = archive.CreateEntry(elyrFileName);
                    using (MemoryStream lyricBody = new MemoryStream(bom))
                    using (Stream ls = lyricEntry.Open())
                    {
                        lyricBody.CopyTo(ls);
                    }

                    ZipArchiveEntry audioEntry = archive.CreateEntry(audioResource.FileName);
                    using (MemoryStream audioBody = new MemoryStream(KFN.GetDataFromResource(audioResource)))
                    using (Stream aus = audioEntry.Open())
                    {
                        audioBody.CopyTo(aus);
                    }
                }

                byte[] emzData = memStream.ToArray();
                if (emzData != null)
                {
                    FileInfo kfnFile = new FileInfo(KFN.FileName);
                    string emzFileName = kfnFile.Name.Substring(0, kfnFile.Name.Length - kfnFile.Extension.Length) + ".emz";
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
            }
        }

        private void ToLRCButton_Click(object sender, RoutedEventArgs e)
        {
            BlockInfo block = iniBlocksView.SelectedItem as BlockInfo;
            FileInfo sourceFile = new FileInfo(KFN.GetAudioSource());
            string lrcFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".lrc";
            string extLRC = KFN.INIToExtLRC(block.Content);
            if (extLRC == null)
            {
                System.Windows.MessageBox.Show((KFN.isError != null)
                    ? KFN.isError
                    : "Fail to create Ext LRC!");
                return;
            }
            Window viewWindow = new ViewWindow(
                lrcFileName,
                extLRC,
                "UTF-8"
            );
            viewWindow.Show();
        }


        private void toELYRButton_Click(object sender, RoutedEventArgs e)
        {
            BlockInfo block = iniBlocksView.SelectedItem as BlockInfo;
            FileInfo sourceFile = new FileInfo(KFN.GetAudioSource());
            string lrcFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".elyr";
            string elyr = KFN.INIToELYR(block.Content);
            if (elyr == null)
            {
                System.Windows.MessageBox.Show((KFN.isError != null)
                    ? KFN.isError
                    : "Fail to create ELYR!");
                return;
            }
            Window viewWindow = new ViewWindow(
                lrcFileName,
                elyr,
                "UTF-8"
            );
            viewWindow.Show();
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

        private void createEMZ2Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
