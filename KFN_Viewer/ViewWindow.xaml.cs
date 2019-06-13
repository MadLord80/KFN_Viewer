using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для ViewWindow.xaml
    /// </summary>
    public partial class ViewWindow : Window
    {
        private double[] textSizes = new double[] { 14, 16, 18, 20, 22, 24, 26, 28, 30 };
        private string fileName;
        private readonly FolderBrowserDialog FolderBrowserDialog = new FolderBrowserDialog();

        public string EditedText
        {
            get { return TextWindow.Text; }
        }

        public ViewWindow(string fileName, string text, string encoding)
        {
            InitializeComponent();

            this.fileName = fileName;

            ViewWindowElement.Title += this.fileName + " (" + encoding + ")";            
            textSizeBox.ItemsSource = textSizes;
            textSizeBox.SelectedIndex = 0;
            TextWindow.FontSize = textSizes[0];
            TextWindow.Text = text;
        }

        private void TextSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TextWindow.FontSize = (double)textSizeBox.SelectedItem;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
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

                byte[] data = Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(1251), Encoding.UTF8.GetBytes(TextWindow.Text));
                using (FileStream fs = new FileStream(exportFolder + "\\" + this.fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(data, 0, data.Length);
                }
                System.Windows.MessageBox.Show("Export OK: " + exportFolder + "\\" + this.fileName);
            }
        }
    }
}
