﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.IO;
using System.Security.Cryptography;
using Mozilla.NUniversalCharDet;

namespace KFN_Viewer
{
    public partial class MainWindow : Window
    {
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private readonly FolderBrowserDialog FolderBrowserDialog = new FolderBrowserDialog();
        private string KFNFile;
        private KFN KFN = new KFN();
        private List<KFN.ResorceFile> resources = new List<KFN.ResorceFile>();
        private Dictionary<string, string> properties = new Dictionary<string, string>();
        private long endOfHeaderOffset;
        
        private int filesEncoding = 0;
        private int filesEncodingAuto = 0;
        private readonly Dictionary<int, string> encodings = new Dictionary<int, string>
        { { 0, "Use auto detect" } };

        public MainWindow()
        {
            InitializeComponent();

            string version = System.Windows.Forms.Application.ProductVersion;
            MainWindowElement.Title += " v." + version.Remove(version.Length - 2);

            foreach (EncodingInfo enc in Encoding.GetEncodings())
            {
                //if (enc.CodePage == 65001 || enc.CodePage == 1251) { continue; }
                encodings.Add(enc.CodePage, enc.DisplayName);
            }
            FilesEncodingElement.DisplayMemberPath = "Value";
            FilesEncodingElement.ItemsSource = encodings;
            FilesEncodingElement.SelectedIndex = 0;
            FilesEncodingElement.IsEnabled = false;

            OpenFileDialog.Filter = "KFN files (*.kfn)|*.kfn|All files (*.*)|*.*";
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
            propertiesView.ItemsSource = null;
            properties.Clear();
            resourcesView.ItemsSource = null;
            resources.Clear();
            filesEncodingAuto = 0;

            fileNameLabel.Content = "KFN file: " + KFNFile;

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

                byte[] prop = new byte[5];
                byte[] propValue = new byte[4];
                int maxProps = 40;
                while (maxProps > 0)
                {
                    fs.Read(prop, 0, prop.Length);
                    string propName = new string(Encoding.UTF8.GetChars(new ArraySegment<byte>(prop, 0, 4).ToArray()));
                    if (propName == "ENDH")
                    {
                        fs.Position += 4;
                        break;
                    }
                    string SpropName = KFN.GetPropDesc(propName);
                    if (prop[4] == 1)
                    {
                        fs.Read(propValue, 0, propValue.Length);
                        if (SpropName == "Genre" && BitConverter.ToUInt32(propValue, 0) == 0xffffffff)
                        {
                            properties.Add(SpropName, "Not set");
                        }
                        else
                        {
                            if (SpropName.Contains("unknown"))
                            {
                                PropertyWindow.Text += SpropName + ": " + BitConverter.ToUInt32(propValue, 0) + "\n";
                            }                            
                            if (propName != SpropName)
                            {
                                properties.Add(SpropName, BitConverter.ToUInt32(propValue, 0).ToString());
                            }
                        }                        
                    }
                    else if (prop[4] == 2)
                    {
                        fs.Read(propValue, 0, propValue.Length);
                        byte[] value = new byte[BitConverter.ToUInt32(propValue, 0)];
                        fs.Read(value, 0, value.Length);
                        if (SpropName == "AES-ECB-128 Key")
                        {
                            string val = (value.Select(b => (int)b).Sum() == 0) 
                                ? "Not present" 
                                : value.Select(b => b.ToString("X2")).Aggregate((s1, s2) => s1 + s2);
                            properties.Add(SpropName, val);
                        }
                        else
                        {
                            if (SpropName.Contains("unknown"))
                            {
                                PropertyWindow.Text += SpropName + ": " + new string(Encoding.UTF8.GetChars(value)) + "\n";
                            }                            
                            if (propName != SpropName)
                            {
                                properties.Add(SpropName, new string(Encoding.UTF8.GetChars(value)));
                            }
                        }
                    }
                    else
                    {
                        PropertyWindow.Text += SpropName + ": unknown block type - " + prop[4] + "!\n";
                        properties.Add(SpropName, "unknown block type - " + prop[4]);
                        return;
                    }
                    maxProps--;
                }
                propertiesView.ItemsSource = properties;

                byte[] numOfFiles = new byte[4];
                fs.Read(numOfFiles, 0, numOfFiles.Length);
                int filesCount = BitConverter.ToInt32(numOfFiles, 0);
                while (filesCount > 0)
                {
                    byte[] fileNameLenght = new byte[4];
                    byte[] fileType = new byte[4];
                    byte[] fileLenght = new byte[4];
                    byte[] fileEncryptedLenght = new byte[4];
                    byte[] fileOffset = new byte[4];
                    byte[] fileEncrypted = new byte[4];

                    fs.Read(fileNameLenght, 0, fileNameLenght.Length);
                    byte[] fileName = new byte[BitConverter.ToUInt32(fileNameLenght, 0)];
                    fs.Read(fileName, 0, fileName.Length);
                    fs.Read(fileType, 0, fileType.Length);
                    fs.Read(fileLenght, 0, fileLenght.Length);
                    fs.Read(fileOffset, 0, fileOffset.Length);
                    fs.Read(fileEncryptedLenght, 0, fileEncryptedLenght.Length);
                    fs.Read(fileEncrypted, 0, fileEncrypted.Length);
                    int encrypted = BitConverter.ToInt32(fileEncrypted, 0);

                    if (KFN.GetFileType(fileType) == "Audio" && filesEncodingAuto == 0)
                    {
                        UniversalDetector Det = new UniversalDetector(null);
                        Det.HandleData(fileName, 0, fileName.Length);
                        Det.DataEnd();
                        string enc = Det.GetDetectedCharset();
                        if (enc != null && enc != "Not supported")
                        {
                            Encoding denc = Encoding.GetEncoding(enc);
                            filesEncodingAuto = denc.CodePage;
                            AutoDetectedEncLabel.Content = denc.EncodingName;
                            if (filesEncoding == 0)
                            {
                                filesEncoding = denc.CodePage;
                                FilesEncodingElement.SelectionChanged -= 
                                    new SelectionChangedEventHandler(FilesEncodingElement_SelectionChanged);
                                FilesEncodingElement.SelectedIndex = FilesEncodingElement.Items
                                    .IndexOf(encodings.Where(e => e.Key == denc.CodePage).First());
                                FilesEncodingElement.SelectionChanged +=
                                    new SelectionChangedEventHandler(FilesEncodingElement_SelectionChanged);
                            }
                        }
                        else
                        {
                            AutoDetectedEncLabel.Content = "No detected";
                        }
                    }

                    int useEncoding = (filesEncoding != 0) ? filesEncoding : filesEncodingAuto;
                    string fName = new string(Encoding.GetEncoding(useEncoding).GetChars(fileName));

                    resources.Add(new KFN.ResorceFile(
                        KFN.GetFileType(fileType),
                        fName,
                        BitConverter.ToInt32(fileEncryptedLenght, 0),
                        BitConverter.ToInt32(fileOffset, 0),
                        (encrypted == 0) ? false : true
                    ));

                    filesCount--;
                }
                endOfHeaderOffset = fs.Position;
                resourcesView.ItemsSource = resources;
                AutoSizeColumns(resourcesView.View as GridView);
            }
            FilesEncodingElement.IsEnabled = true;
        }

        private void FilesEncodingElement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            KeyValuePair<int, string> selectedEncoding = (KeyValuePair<int, string>)FilesEncodingElement.SelectedItem;
            filesEncoding = selectedEncoding.Key;
            if (KFNFile != null) { ReadFile(); }
        }

        private void FilesEncodingElement_DropDownClosed(object sender, EventArgs e)
        {
            KeyValuePair<int, string> selectedEncoding = (KeyValuePair<int, string>)FilesEncodingElement.SelectedItem;
            filesEncoding = selectedEncoding.Key;
            if (KFNFile != null) { ReadFile(); }
        }

        private void ExportResource(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button b = sender as System.Windows.Controls.Button;
            KFN.ResorceFile resource = b.CommandParameter as KFN.ResorceFile;

            FolderBrowserDialog.SelectedPath = new FileInfo(KFNFile).DirectoryName;
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

                byte[] data = new byte[resource.FileLength];
                using (FileStream fs = new FileStream(KFNFile, FileMode.Open, FileAccess.Read))
                {
                    fs.Position = endOfHeaderOffset + resource.FileOffset;
                    fs.Read(data, 0, data.Length);
                }

                if (resource.IsEncrypted)
                {
                    byte[] Key = Enumerable.Range(0, properties["AES-ECB-128 Key"].Length)
                        .Where(x => x % 2 == 0)
                        .Select(x => Convert.ToByte(properties["AES-ECB-128 Key"].Substring(x, 2), 16))
                        .ToArray();
                    data = DecryptData(data, Key);
                }

                using (FileStream fs = new FileStream(exportFolder + "\\" + resource.FileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(data, 0, data.Length);
                }
                System.Windows.MessageBox.Show("Export OK: " + exportFolder + "\\" + resource.FileName);
            }
        }

        private void AutoSizeColumns(GridView gv)
        {
            if (gv != null)
            {
                foreach (var c in gv.Columns)
                {
                    // Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
                    // i.e. it is the same code that is executed when the gripper is double clicked
                    if (double.IsNaN(c.Width))
                    {
                        c.Width = c.ActualWidth;
                    }
                    c.Width = double.NaN;
                }
            }
        }

        private byte[] DecryptData(byte[] data, byte[] Key)
        {
            RijndaelManaged aes = new RijndaelManaged();
            aes.KeySize = 128;
            aes.Padding = PaddingMode.None;
            aes.Mode = CipherMode.ECB;
            using (ICryptoTransform decrypt = aes.CreateDecryptor(Key, null))
            {
                byte[] dest = decrypt.TransformFinalBlock(data, 0, data.Length);
                decrypt.Dispose();
                //return Encoding.UTF8.GetString(dest);
                return dest;
            }
        }

        // TODO (maybe)
        private void Test(object sender, RoutedEventArgs e)
        {
            //Window playerWindow = new PlayWindow("D:\\DJ Piligrim LIVE @ Disco MCLUB (Augsburg) - 20. Mai 2009.avi");
            //playerWindow.Show();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
        }

        // karaore text
        //https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/how-to-create-outlined-text
        //https://ru.stackoverflow.com/questions/630777/%D0%97%D0%B0%D0%BA%D1%80%D0%B0%D1%81%D0%B8%D1%82%D1%8C-%D1%82%D0%B5%D0%BA%D1%81%D1%82-%D1%81%D0%BB%D0%BE%D0%B2%D0%BE-%D0%B1%D1%83%D0%BA%D0%B2%D1%83
    }
}
