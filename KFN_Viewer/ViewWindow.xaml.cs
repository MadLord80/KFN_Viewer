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
        
        public ViewWindow(string fileName, string text, string encoding)
        {
            InitializeComponent();

            ViewWindowElement.Title += fileName + " (" + encoding + ")";            
            textSizeBox.ItemsSource = textSizes;
            textSizeBox.SelectedIndex = 0;
            TextWindow.FontSize = textSizes[0];
            TextWindow.Text = text;
        }

        private void TextSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TextWindow.FontSize = (double)textSizeBox.SelectedItem;
        }
    }
}
