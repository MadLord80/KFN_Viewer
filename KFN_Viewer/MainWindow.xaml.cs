using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.IO;
using System.Drawing;
using System.Security.Cryptography;

namespace KFN_Viewer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private readonly FolderBrowserDialog FolderBrowserDialog = new FolderBrowserDialog();
        private string KFNFile;
        private KFN KFN = new KFN();
        private List<KFN.ResorceFile> resources = new List<KFN.ResorceFile>();
        private Dictionary<string, string> properties = new Dictionary<string, string>();
        private long endOfHeaderOffset;

        private int filesEncoding = 65001;
        // https://docs.microsoft.com/ru-ru/dotnet/api/system.text.encodinginfo.name?view=netframework-4.5
        private readonly Dictionary<int, string> encodings = new Dictionary<int, string>
        {
            {65001, "utf-8"},
            {1251,  "windows-1251"}
        };

        public MainWindow()
        {
            InitializeComponent();

            string version = System.Windows.Forms.Application.ProductVersion;
            MainWindowElement.Title += " v." + version.Remove(version.Length - 2);

            FilesEncodingElement.DisplayMemberPath = "Value";
            FilesEncodingElement.ItemsSource = encodings;
            FilesEncodingElement.SelectedIndex = 0;

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

            fileNameLabel.Content = "KFN file: " + KFNFile;
            //PropertyWindow.Text = "File: " + KFNFile + "\nHeader blocks:\n";

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
                            //PropertyWindow.Text += SpropName + ": Not set\n";
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
                            //PropertyWindow.Text += SpropName + ": " + val + "\n";
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
                //PropertyWindow.Text += "Files (" + filesCount + "):\n";
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

                    string fName = new string(Encoding.GetEncoding(filesEncoding).GetChars(fileName));
                    //PropertyWindow.Text += KFN.GetFileType(fileType) + ": "
                    //    + fName
                    //    + ", length1=" + BitConverter.ToUInt32(fileLenght, 0)
                    //    + ", length2=" + BitConverter.ToUInt32(fileEncryptedLenght, 0)
                    //    + ", encrypted - " + ((encrypted == 1) ? "yes\n" : ((encrypted == 0) ? "no\n" : "unknown (" + encrypted + ")\n"));

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
        }

        private void FilesEncodingElement_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

                using (FileStream fs = new FileStream(exportFolder + resource.FileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(data, 0, data.Length);
                }
                System.Windows.MessageBox.Show("Export OK: " + exportFolder + resource.FileName);
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

        // encryption
        //https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes?view=netframework-4.5

        private void TestEnc(object sender, RoutedEventArgs e)
        {
            //string plainText = "test text";
            KFNFile = "D:\\что такое осень lock.kfn";
            ReadFile();
            KFN.ResorceFile rf = resources.Where(r => r.FileType == "Lyrics").First();
            byte[] data = new byte[rf.FileLength];
            using (FileStream fs = new FileStream(KFNFile, FileMode.Open, FileAccess.Read))
            {
                fs.Position = endOfHeaderOffset + rf.FileOffset;
                fs.Read(data, 0, data.Length);
            }
            //string plainText = new string(Encoding.UTF8.GetChars(data));

            //byte[] Key = { 0x6D, 0xF0, 0xD2, 0xB8, 0xC1, 0xD7, 0xCF, 0xC9, 0x2F, 0xAE, 0xB0, 0xEF, 0x25, 0xE3, 0xB4, 0x8A };
            byte[] Key = Enumerable.Range(0, properties["AES-ECB-128 Key"].Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(properties["AES-ECB-128 Key"].Substring(x, 2), 16))
                    .ToArray();
            byte[] IV = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            //byte[] encrypted = EncryptStringToBytes_Aes(plainText, Key, IV);
            string decrypted = DecryptData(data, Key, IV);
        }

        private string DecryptData(byte[] data, byte[] Key, byte[] IV)
        //private byte[] DecryptData(byte[] data)
        {
            //byte[] cipherText, byte[] Key, byte[] IV
            // Check arguments.
            //if (cipherText == null || cipherText.Length <= 0)
            //    throw new ArgumentNullException("cipherText");
            //if (Key == null || Key.Length <= 0)
            //    throw new ArgumentNullException("Key");
            //if (IV == null || IV.Length <= 0)
            //    throw new ArgumentNullException("IV");

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;
            //byte[] decrypted = { };

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                //aesAlg.Key = Enumerable.Range(0, properties["AES-ECB-128 Key"].Length)
                //    .Where(x => x % 2 == 0)
                //    .Select(x => Convert.ToByte(properties["AES-ECB-128 Key"].Substring(x, 2), 16))
                //    .ToArray();
                aesAlg.IV = IV;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                //ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, new byte[] { 0 });

                // Create the streams used for decryption.
                //using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                using (MemoryStream msDecrypt = new MemoryStream(data))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                            //decrypted = srDecrypt.r
                        }
                    }
                }

            }
            return plaintext;
            //return decrypted;
        }

        static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            //string plainText, byte[] Key, byte[] IV
            
            // Check arguments.
            //if (plainText == null || plainText.Length <= 0)
            //    throw new ArgumentNullException("plainText");
            //if (Key == null || Key.Length <= 0)
            //    throw new ArgumentNullException("Key");
            //if (IV == null || IV.Length <= 0)
            //    throw new ArgumentNullException("IV");
            byte[] encrypted;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                //ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, new byte[] { 0 });

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            // Return the encrypted bytes from the memory stream.
            return encrypted;

        }

        // karaore text
        //https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/how-to-create-outlined-text
        //https://ru.stackoverflow.com/questions/630777/%D0%97%D0%B0%D0%BA%D1%80%D0%B0%D1%81%D0%B8%D1%82%D1%8C-%D1%82%D0%B5%D0%BA%D1%81%D1%82-%D1%81%D0%BB%D0%BE%D0%B2%D0%BE-%D0%B1%D1%83%D0%BA%D0%B2%D1%83
    }
}
