using System;
using System.Windows;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using IniParser.Model;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для SongINIWindow.xaml
    /// </summary>
    public partial class SongINIWindow : Window
    {
        private KFN KFN;

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
            KFN.ResorceFile resource = KFN.Resorces.Where(r => r.FileName == "Song.ini").First();
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
        }

        private void ToLRCButton_Click(object sender, RoutedEventArgs e)
        {
            BlockInfo block = iniBlocksView.SelectedItem as BlockInfo;
            FileInfo sourceFile = new FileInfo(KFN.GetAudioSource());
            string lrcFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".lrc";
            string extLRC = KFN.INIToExtLRC(block.Content);
            Window viewWindow = new ViewWindow(
                lrcFileName,
                extLRC,
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
    }
}
