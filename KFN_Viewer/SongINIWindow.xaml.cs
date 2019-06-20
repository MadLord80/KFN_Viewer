using System.Windows;
using System.Linq;
using System.Text;
using System.Windows.Controls;

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
            
            this.ParseINI(KFN);
        }

        private void ParseINI(KFN KFN)
        {
            KFN.ResourceFile resource = KFN.Resources.Where(r => r.FileName == "Song.ini").First();
            byte[] data = KFN.GetDataFromResource(resource);            
            string iniText = new string(Encoding.UTF8.GetChars(data));

            SongINI sINI = new SongINI(iniText);

            iniBlocksView.ItemsSource = sINI.Blocks;
            this.AutoSizeColumns(iniBlocksView.View as GridView);
        }

        private void IniBlocksView_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SongINI.BlockInfo block = iniBlocksView.SelectedItem as SongINI.BlockInfo;
            blockContent.Text = block.Content;
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
