using System;
using System.Windows;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using IniParser.Model;
using System.Collections.Generic;
using System.Windows.Controls;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для SongINIWindow.xaml
    /// </summary>
    public partial class SongINIWindow : Window
    {
        public SongINIWindow(KFN KFN)
        {
            InitializeComponent();

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

            public BlockInfo(string name, string id, string type, string content)
            {
                this.name = name;
                this.id = id;
                this.type = type;
                this.content = content;
            }
        }

        private void ParseINI(KFN KFN)
        {
            var parser = new IniParser.Parser.IniDataParser();
            KFN.ResorceFile resource = KFN.Resorces.Where(r => r.FileName == "Song.ini").First();
            string iniText = new string(Encoding.UTF8.GetChars(KFN.GetDataFromResource(resource)));
            IniData iniData = parser.Parse(iniText);

            List<BlockInfo> blocksData = new List<BlockInfo>();
            foreach (SectionData block in iniData.Sections)
            {
                string blockId = block.Keys["ID"];
                blocksData.Add(new BlockInfo(
                    block.SectionName,
                    blockId,
                    (blockId != null) ? KFN.GetIniBlockType(Convert.ToInt32(blockId)) : "",
                    block.ToString()
                ));
            }

            iniBlocksView.ItemsSource = blocksData;
            this.AutoSizeColumns(iniBlocksView.View as GridView);
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
