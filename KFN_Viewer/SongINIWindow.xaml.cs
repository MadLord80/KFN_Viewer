﻿using System;
using System.Windows;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.IO;
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
        private string editedText = null;
        
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
            createEMZ2Button.IsEnabled = false;

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
            toLRCButton.IsEnabled = (block.Id == "1" || block.Id == "2") ? true : false;
            toELYRButton.IsEnabled = (block.Id == "1" || block.Id == "2") ? true : false;
            createEMZButton.IsEnabled = (block.Id == "1" || block.Id == "2") ? true : false;
            KFN.ResourceFile video = KFN.GetVideoResource();
            createEMZ2Button.IsEnabled = ((block.Id == "1" || block.Id == "2") && video != null) ? true : false;
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
            SongINI.BlockInfo block = iniBlocksView.SelectedItem as SongINI.BlockInfo;
            byte[] emzData = KFN.createEMZ((this.editedText == null) ? block.Content : this.editedText, withVideo);
            if (emzData == null)
            {
                System.Windows.MessageBox.Show((KFN.isError != null)
                    ? KFN.isError
                    : "Fail to create EMZ!");
                return;
            }

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

        private void ToLRCButton_Click(object sender, RoutedEventArgs e)
        {
            SongINI.BlockInfo block = iniBlocksView.SelectedItem as SongINI.BlockInfo;
            FileInfo sourceFile = new FileInfo(KFN.GetAudioSourceName());
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
            SongINI.BlockInfo block = iniBlocksView.SelectedItem as SongINI.BlockInfo;
            FileInfo sourceFile = new FileInfo(KFN.GetAudioSourceName());
            string lrcFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".elyr";
            string elyr = KFN.INIToELYR(block.Content);
            if (elyr == null)
            {
                System.Windows.MessageBox.Show((KFN.isError != null)
                    ? KFN.isError
                    : "Fail to create ELYR!");
                return;
            }
            ViewWindow viewWindow = new ViewWindow(
                lrcFileName,
                elyr,
                "UTF-8",
                this.editedText
            );
            viewWindow.ShowDialog();
            this.editedText = viewWindow.EditedText;
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
