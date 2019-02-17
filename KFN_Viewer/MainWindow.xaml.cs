using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private string KFNFile;
        private KFN KFN = new KFN();
        // 1251	Windows 1251
        // 65001	UTF-8
        private int filesEncoding = 65001;

        public MainWindow()
        {
            InitializeComponent();

            string version = System.Windows.Forms.Application.ProductVersion;
            MainWindowElement.Title += " v." + version.Remove(version.Length - 2);

            // https://docs.microsoft.com/ru-ru/dotnet/api/system.text.encodinginfo.name?view=netframework-4.5
            //FilesEncodingElement.ItemsSource = new 
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                KFNFile = OpenFileDialog.FileName;
                ReadFile();
            }
        }

        private void ReadFile()
        {
            PropertyWindow.Text = "File: " + KFNFile + "\nHeader blocks:\n";
            using (FileStream fs = new FileStream(KFNFile, FileMode.Open, FileAccess.Read))
            {
                byte[] signature = new byte[4];
                fs.Read(signature, 0, signature.Length);
                string sign = new string(Encoding.UTF8.GetChars(signature));
                if (sign != "KFNB")
                {
                    System.Windows.MessageBox.Show("Invalid KFN signature!");
                    return;
                }

                byte[] block = new byte[5];
                byte[] blockValue = new byte[4];
                int maxBlocks = 20;
                while (maxBlocks > 0)
                {
                    fs.Read(block, 0, block.Length);
                    string blockName = new string(Encoding.UTF8.GetChars(new ArraySegment<byte>(block, 0, 4).ToArray()));
                    if (blockName == "ENDH")
                    {
                        fs.Position += 4;
                        break;
                    }
                    blockName = KFN.GetBlockDesc(blockName);
                    if (block[4] == 1)
                    {
                        fs.Read(blockValue, 0, blockValue.Length);
                        PropertyWindow.Text += blockName + ": " + BitConverter.ToUInt32(blockValue, 0) + "\n";
                    }
                    else if (block[4] == 2)
                    {
                        fs.Read(blockValue, 0, blockValue.Length);
                        byte[] value = new byte[BitConverter.ToUInt32(blockValue, 0)];
                        fs.Read(value, 0, value.Length);
                        PropertyWindow.Text += blockName + ": " + new string(Encoding.UTF8.GetChars(value)) + "\n";
                    }
                    else
                    {
                        PropertyWindow.Text += blockName + ": unknown block type - " + block[4] + "!\n";
                        return;
                    }
                    maxBlocks--;
                }

                byte[] numOfFiles = new byte[4];
                fs.Read(numOfFiles, 0, numOfFiles.Length);
                int filesCount = BitConverter.ToInt32(numOfFiles, 0);
                PropertyWindow.Text += "Files (" + filesCount + "):\n";
                while (filesCount > 0)
                {
                    byte[] fileNameLenght = new byte[4];
                    byte[] fileType = new byte[4];
                    byte[] fileLenght1 = new byte[4];
                    byte[] fileLenght2 = new byte[4];
                    byte[] fileOffset = new byte[4];
                    byte[] fileEncrypted = new byte[4];

                    fs.Read(fileNameLenght, 0, fileNameLenght.Length);
                    byte[] fileName = new byte[BitConverter.ToUInt32(fileNameLenght, 0)];
                    fs.Read(fileName, 0, fileName.Length);
                    fs.Read(fileType, 0, fileType.Length);
                    fs.Read(fileLenght1, 0, fileLenght1.Length);
                    fs.Read(fileOffset, 0, fileOffset.Length);
                    fs.Read(fileLenght2, 0, fileLenght2.Length);
                    fs.Read(fileEncrypted, 0, fileEncrypted.Length);
                    int encrypted = BitConverter.ToInt32(fileEncrypted, 0);

                    PropertyWindow.Text += KFN.GetFileType(fileType) + ": "
                        + new string(Encoding.UTF8.GetChars(fileName))
                        + ", length1=" + BitConverter.ToUInt32(fileLenght1, 0)
                        + ", length2=" + BitConverter.ToUInt32(fileLenght2, 0)
                        + ", encrypted - " + ((encrypted == 1) ? "yes\n" : ((encrypted == 0) ? "no\n" : "unknown (" + encrypted + ")\n"));

                    filesCount--;
                }
            }
        }
    }
}
