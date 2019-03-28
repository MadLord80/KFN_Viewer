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
using System.Text.RegularExpressions;
using System.IO.Compression;

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

        private int filesEncodingAuto = 20127;
        private readonly Dictionary<int, string> encodings = new Dictionary<int, string>
        { { 0, "Use auto detect" } };

        public MainWindow()
        {
            InitializeComponent();

            string version = System.Windows.Forms.Application.ProductVersion;
            MainWindowElement.Title += " v." + version.Remove(version.Length - 2);

            foreach (EncodingInfo enc in Encoding.GetEncodings())
            {
                encodings.Add(enc.CodePage, enc.CodePage + ": " + enc.DisplayName);
            }
            FilesEncodingElement.DisplayMemberPath = "Value";
            FilesEncodingElement.ItemsSource = encodings;
            FilesEncodingElement.SelectedIndex = 0;
            FilesEncodingElement.IsEnabled = false;

            OpenFileDialog.Filter = "KFN files (*.kfn)|*.kfn|All files (*.*)|*.*";

            ResourceViewInit();
            
#if !DEBUG
            testButton.Visibility = Visibility.Hidden;               
#endif
        }

        private void ResourceViewInit()
        {
            GridView resourceGrid = new GridView
            {
                ColumnHeaderContainerStyle =
                    System.Windows.Application.Current.Resources["GridViewColumnHeaderStyle"] as Style
            };
            resourceGrid.Columns.Add(new GridViewColumn()
            {
                Header = "AES Enc",
                Width = 60,
                DisplayMemberBinding = new System.Windows.Data.Binding("IsEncrypted")
            });
            resourceGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Type",
                DisplayMemberBinding = new System.Windows.Data.Binding("FileType")
            });
            resourceGrid.Columns.Add(new GridViewColumn()
            {
                Header = "Name",
                DisplayMemberBinding = new System.Windows.Data.Binding("FileName")
            });

            resourcesView.View = resourceGrid;

            System.Windows.Controls.ContextMenu context = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem exportItem = new System.Windows.Controls.MenuItem() { Header = "Export" };
            exportItem.Click += ExportResourceButtonClick;
            context.Items.Add(exportItem);
            resourcesView.ContextMenu = context;
            resourcesView.ContextMenuOpening += resourcesViewContext;
        }

        private void resourcesViewContext(object sender, ContextMenuEventArgs e)
        {
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;
            System.Windows.Controls.ContextMenu rvcontext = resourcesView.ContextMenu;
            if (rvcontext.Items.Count > 1)
            {
                while (rvcontext.Items.Count > 1)
                {
                    rvcontext.Items.RemoveAt(1);
                }
            }

            if (resource.FileType == "Text" || resource.FileType == "Lyrics" || resource.FileType == "Image")
            {
                System.Windows.Controls.MenuItem viewItem = new System.Windows.Controls.MenuItem() { Header = "View" };
                viewItem.Click += ViewResourceButtonClick;
                rvcontext.Items.Add(viewItem);

                if (resource.FileType == "Lyrics")
                {
                    System.Windows.Controls.MenuItem toLRCItem = new System.Windows.Controls.MenuItem() { Header = "Convert to Extended LRC" };
                    toLRCItem.Click += ExportToLrcButtonClick;
                    rvcontext.Items.Add(toLRCItem);
                    System.Windows.Controls.MenuItem toELYRItem = new System.Windows.Controls.MenuItem() { Header = "Convert to ELYR" };
                    toELYRItem.Click += ExportToElyrButtonClick;
                    rvcontext.Items.Add(toELYRItem);
                }
            }

            string sourceName = GetAudioSource();
            if ((resource.FileType == "Audio" && sourceName != null && resource.FileName == sourceName) || resource.FileType == "Lyrics")
            {
                System.Windows.Controls.MenuItem toEMZItem = new System.Windows.Controls.MenuItem() { Header = "Create EMZ" };
                toEMZItem.Click += CreateEMZButtonClick;
                rvcontext.Items.Add(toEMZItem);
            }
        }

        private void CreateEMZButtonClick(object sender, RoutedEventArgs e)
        {
            string audioFile = GetAudioSource();
            if (audioFile == null) { return; }
            KFN.ResorceFile audioResource = resources.Where(r => r.FileName == audioFile).FirstOrDefault();
            if (audioResource == null) { return; }

            KFN.ResorceFile lyricResource = resources.Where(r => r.FileName == "Song.ini").FirstOrDefault();
            if (lyricResource == null) { return; }

            FileInfo sourceFile = new FileInfo(audioFile);
            string elyrFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".elyr";

            string elyrText = INIToELYR(new string(Encoding.UTF8.GetChars(GetDataFromResource(lyricResource))));

            //byte[] bom = new byte[] { 0xFF, 0xFE };
            byte[] bom = new byte[0];
            //string elyrHeader = "encore.lg-karaoke.ru ver=02 crc=00000000 \r\n";
            //byte[] elyr = Encoding.Convert(Encoding.UTF8, Encoding.BigEndianUnicode, Encoding.UTF8.GetBytes(elyrHeader + elyrText));
            byte[] elyr = Encoding.Convert(Encoding.UTF8, Encoding.BigEndianUnicode, Encoding.UTF8.GetBytes(elyrText));
            Array.Resize(ref bom, bom.Length + elyr.Length - 1);
            Array.Copy(elyr, 1, bom, 2, elyr.Length - 1);
            using (MemoryStream memStream = new MemoryStream())
            {
                using (ZipArchive archive = new ZipArchive(memStream, ZipArchiveMode.Create, true))
                {
                    ZipArchiveEntry lyricEntry = archive.CreateEntry(elyrFileName);
                    using (MemoryStream lyricBody = new MemoryStream(bom))
                    using (Stream ls = lyricEntry.Open())
                    {
                        lyricBody.CopyTo(ls);
                    }

                    ZipArchiveEntry audioEntry = archive.CreateEntry(audioResource.FileName);
                    using (MemoryStream audioBody = new MemoryStream(GetDataFromResource(audioResource)))
                    using (Stream aus = audioEntry.Open())
                    {
                        audioBody.CopyTo(aus);
                    }
                }

                using (FileStream fs = new FileStream(@"d:\test22.zip", FileMode.Create))
                {
                    memStream.Seek(0, SeekOrigin.Begin);
                    memStream.CopyTo(fs);
                }
            }
            System.Windows.MessageBox.Show("OK");
        }

        private string GetAudioSource()
        {
            if (properties.Count == 0) { return null; }
            //1,I,ddt_-_chto_takoe_osen'.mp3
            KeyValuePair<string, string> sourceProp = properties.Where(kv => kv.Key == "Source").FirstOrDefault();
            return sourceProp.Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Last();
        }
        
        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                KFNFile = OpenFileDialog.FileName;
                filesEncodingAuto = 20127;
                ReadFile();
            }
        }

        private void ReadFile(int filesEncoding = 0)
        {
            propertiesView.ItemsSource = null;
            properties.Clear();
            resourcesView.ItemsSource = null;
            resources.Clear();

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

                    if (filesEncoding == 0 && filesEncodingAuto == 20127)
                    {
                        UniversalDetector Det = new UniversalDetector(null);
                        Det.HandleData(fileName, 0, fileName.Length);
                        Det.DataEnd();
                        string enc = Det.GetDetectedCharset();
                        if (enc != null && enc != "Not supported")
                        {
                            // fix encoding for 1251 upper case and MAC
                            if (enc == "KOI8-R" || enc == "X-MAC-CYRILLIC") { enc = "WINDOWS-1251"; }
                            Encoding denc = Encoding.GetEncoding(enc);
                            filesEncodingAuto = denc.CodePage;
                            AutoDetectedEncLabel.Content = denc.CodePage + ": " + denc.EncodingName;
                        }
                        else if (enc == null)
                        {
                            Encoding denc = Encoding.GetEncoding(filesEncodingAuto);
                            AutoDetectedEncLabel.Content = denc.CodePage + ": " + denc.EncodingName;
                        }
                        else
                        {
                            AutoDetectedEncLabel.Content = "No supported: use " + Encoding.GetEncoding(filesEncodingAuto).EncodingName;
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
            if (resources.Count > 1) { ExportAllButton.IsEnabled = true; }
        }

        private void FilesEncodingElement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            KeyValuePair<int, string> selectedEncoding = (KeyValuePair<int, string>)FilesEncodingElement.SelectedItem;
            if (KFNFile != null) { ReadFile(selectedEncoding.Key); }
        }

        private void ExportAllButton_Click(object sender, RoutedEventArgs e)
        {
            FileInfo kfnfile = new FileInfo(KFNFile);
            string KFNFileDir = kfnfile.DirectoryName;
            string KFNNameDir = kfnfile.Name.Substring(0, kfnfile.Name.Length - kfnfile.Extension.Length);
            
            FolderBrowserDialog.SelectedPath = KFNFileDir;
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

                exportFolder += "\\" + KFNNameDir;
                if (Directory.Exists(exportFolder)) { Directory.Delete(exportFolder, true); }
                Directory.CreateDirectory(exportFolder);

                foreach (KFN.ResorceFile resource in resources)
                {
                    ExportResourceToFile(resource, exportFolder);
                    if (resource.FileType == "Lyrics")
                    {
                        byte[] data = GetDataFromResource(resource);
                        // try to convert to Extended LRC and Elyr
                        string lrcText = INIToExtLRC(new string(Encoding.UTF8.GetChars(data)));
                        string elyrText = INIToELYR(new string(Encoding.UTF8.GetChars(data)));
                        if (lrcText != null)
                        {
                            string audioSource = GetAudioSource();
                            string sourceName = (audioSource != null) ? audioSource : resource.FileName;
                            FileInfo sourceFile = new FileInfo(sourceName);
                            string lrcFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".lrc";
                            string elyrFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".elyr";

                            ExportTextToFile(
                                lrcFileName, 
                                Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(1251), Encoding.UTF8.GetBytes(lrcText)),
                                exportFolder
                            );

                            byte[] bom = new byte[] { 0xFF, 0xFE };
                            string elyrHeader = "encore.lg-karaoke.ru ver=02 crc=00000000 \r\n";
                            byte[] elyr = Encoding.Convert(Encoding.UTF8, Encoding.BigEndianUnicode, Encoding.UTF8.GetBytes(elyrHeader + elyrText));
                            Array.Resize(ref bom, bom.Length + elyr.Length - 1);
                            Array.Copy(elyr, 1, bom, 2, elyr.Length - 1);
                            ExportTextToFile(
                                elyrFileName,
                                bom,
                                exportFolder
                            );
                        }
                    }
                }
                System.Windows.MessageBox.Show("Export OK: " + exportFolder);
            }
        }

        public void ExportToLrcButtonClick(object sender, RoutedEventArgs e)
        {
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;

            byte[] data = GetDataFromResource(resource);

            string textStrings = INIToExtLRC(new string(Encoding.UTF8.GetChars(data)));
            if (textStrings == null) { return; }

            Window viewWindow = new ViewWindow(
                resource.FileName,
                textStrings,
                Encoding.GetEncodings().Where(en => en.CodePage == 65001).First().DisplayName
            );
            viewWindow.Show();
        }

        public void ExportToElyrButtonClick(object sender, RoutedEventArgs e)
        {
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;

            byte[] data = GetDataFromResource(resource);

            string textStrings = INIToELYR(new string(Encoding.UTF8.GetChars(data)));
            if (textStrings == null) { return; }

            Window viewWindow = new ViewWindow(
                resource.FileName,
                textStrings,
                Encoding.GetEncodings().Where(en => en.CodePage == 65001).First().DisplayName
            );
            viewWindow.Show();
        }

        private string INIToELYR(string iniText)
        {
            Regex textRegex = new Regex(@"^Text[0-9]+=(.+)");
            Regex syncRegex = new Regex(@"^Sync[0-9]+=([0-9,]+)");
            string[] words = { };
            int[] timings = { };
            int lines = 0;
            foreach (string str in iniText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match texts = textRegex.Match(str);
                Match syncs = syncRegex.Match(str);
                if (texts.Groups.Count > 1)
                {
                    string textLine = texts.Groups[1].Value;
                    textLine = textLine.Replace(" ", " /");
                    string[] linewords = textLine.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                    // + end of line
                    Array.Resize(ref words, words.Length + linewords.Length + 1);
                    Array.Copy(linewords, 0, words, words.Length - linewords.Length - 1, linewords.Length);
                    lines++;
                }
                else if (syncs.Groups.Count > 1)
                {
                    string songLine = syncs.Groups[1].Value;
                    int[] linetimes = songLine.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s)).ToArray();
                    Array.Resize(ref timings, timings.Length + linetimes.Length);
                    Array.Copy(linetimes, 0, timings, timings.Length - linetimes.Length, linetimes.Length);
                }
            }

            if (timings.Length < words.Length - lines)
            {
                System.Windows.MessageBox.Show("Fail convert: words - " + words.Length + ", timings - " + timings.Length);
                return null;
            }

            if (words.Length == 0) { return null; }
            bool newLine = false;
            int timeIndex = 1;
            int timing = timings[0] * 10;
            string elyrText = timing + ":" + timing + "=\\" + words[0] + "\r\n";
            for (int i = 1; i < words.Length; i++)
            {
                if (!newLine)
                {
                    timing = timings[timeIndex] * 10;
                    elyrText += timing + ":" + timing + "=";
                }

                if (words[i] != null)
                {
                    elyrText += words[i] + "\r\n";
                    newLine = false;
                    if (i < words.Length - 2) { timeIndex++; }
                }
                else
                {
                    elyrText += "\\";
                    newLine = true;
                }
            }

            return elyrText;
        }

        private string INIToExtLRC(string iniText)
        {
            Regex textRegex = new Regex(@"^Text[0-9]+=(.+)");
            Regex syncRegex = new Regex(@"^Sync[0-9]+=([0-9,]+)");
            string[] words = { };
            int[] timings = { };
            int lines = 0;
            foreach (string str in iniText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match texts = textRegex.Match(str);
                Match syncs = syncRegex.Match(str);
                if (texts.Groups.Count > 1)
                {
                    string textLine = texts.Groups[1].Value;
                    textLine = textLine.Replace(" ", " /");
                    string[] linewords = textLine.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                    // + end of line
                    Array.Resize(ref words, words.Length + linewords.Length + 1);
                    Array.Copy(linewords, 0, words, words.Length - linewords.Length - 1, linewords.Length);
                    lines++;
                }
                else if (syncs.Groups.Count > 1)
                {
                    string songLine = syncs.Groups[1].Value;
                    int[] linetimes = songLine.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s)).ToArray();
                    Array.Resize(ref timings, timings.Length + linetimes.Length);
                    Array.Copy(linetimes, 0, timings, timings.Length - linetimes.Length, linetimes.Length);
                }
            }

            if (timings.Length < words.Length - lines)
            {
                System.Windows.MessageBox.Show("Fail convert: words - " + words.Length + ", timings - " + timings.Length);
                return null;
            }

            string lrcText = "";
            if (words.Length == 0) { return null; }
            bool newLine = true;
            int timeIndex = 0;
            for (int i = 0; i < words.Length; i++)
            {
                string startTag = (newLine) ? "[" : "<";
                string endTag = (newLine) ? "]" : ">";
                
                // in end of line: +45 msec
                int timing = (words[i] != null) ? timings[timeIndex] : timings[timeIndex - 1] + 45;
                decimal time = Convert.ToDecimal(timing);
                decimal min = Math.Truncate(time / 6000);
                decimal sec = Math.Truncate((time - min * 6000) / 100);
                decimal msec = Math.Truncate(time - (min * 6000 + sec * 100));

                lrcText += startTag + String.Format("{0:D2}", (int)min) + ":"
                        + String.Format("{0:D2}", (int)sec) + "."
                        + String.Format("{0:D2}", (int)msec) + endTag;

                if (words[i] != null)
                {
                    lrcText += words[i];
                    newLine = false;
                    timeIndex++;
                }
                else
                {
                    lrcText += "\n";
                    newLine = true;
                }                
            }
            KeyValuePair<string, string> artistProp = properties.Where(kv => kv.Key == "Artist").FirstOrDefault();
            KeyValuePair<string, string> titleProp = properties.Where(kv => kv.Key == "Title").FirstOrDefault();
            if (titleProp.Value != null) { lrcText = "[ti:" + titleProp.Value + "]\n" + lrcText; }
            if (artistProp.Value != null) { lrcText = "[ar:" + artistProp.Value + "]\n" + lrcText; }

            return lrcText;
        }

        public void ViewResourceButtonClick(object sender, RoutedEventArgs e)
        {
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;

            if (resource.FileType == "Text" || resource.FileType == "Lyrics")
            {
                byte[] data = GetDataFromResource(resource);

                //UTF-8
                int detEncoding = 65001;
                if (resource.FileType == "Text")
                {
                    UniversalDetector Det = new UniversalDetector(null);
                    Det.HandleData(data, 0, data.Length);
                    Det.DataEnd();
                    string enc = Det.GetDetectedCharset();
                    if (enc != null && enc != "Not supported")
                    {
                        // fix encoding for 1251 upper case and MAC
                        //if (enc == "KOI8-R" || enc == "X-MAC-CYRILLIC") { enc = "WINDOWS-1251"; }
                        Encoding denc = Encoding.GetEncoding(enc);
                        detEncoding = denc.CodePage;
                    }
                }

                string text = new string(Encoding.GetEncoding(detEncoding).GetChars(data));
                Window viewWindow = new ViewWindow(
                    resource.FileName, 
                    text, 
                    Encoding.GetEncodings().Where(en => en.CodePage == detEncoding).First().DisplayName
                );
                viewWindow.Show();
            }
            else if (resource.FileType == "Image")
            {
                byte[] data = GetDataFromResource(resource);

                Window viewWindow = new ImageWindow(resource.FileName, data);
                viewWindow.Show();
            }
        }

        private void ExportResourceButtonClick(object sender, RoutedEventArgs e)
        {
            KFN.ResorceFile resource = resourcesView.SelectedItem as KFN.ResorceFile;

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

                ExportResourceToFile(resource, exportFolder);
                System.Windows.MessageBox.Show("Export OK: " + exportFolder + "\\" + resource.FileName);
            }
        }

        private void ExportResourceToFile(KFN.ResorceFile resource, string folder)
        {
            byte[] data = GetDataFromResource(resource);

            using (FileStream fs = new FileStream(folder + "\\" + resource.FileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(data, 0, data.Length);
            }
        }
        private void ExportTextToFile(string fileName, byte[] text, string folder)
        {
            using (FileStream fs = new FileStream(folder + "\\" + fileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(text, 0, text.Length);
            }
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

        private byte[] GetDataFromResource(KFN.ResorceFile resource)
        {
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
            return data;
        }

        private byte[] DecryptData(byte[] data, byte[] Key)
        {
            RijndaelManaged aes = new RijndaelManaged
            {
                KeySize = 128,
                Padding = PaddingMode.None,
                Mode = CipherMode.ECB
            };
            using (ICryptoTransform decrypt = aes.CreateDecryptor(Key, null))
            {
                byte[] dest = decrypt.TransformFinalBlock(data, 0, data.Length);
                decrypt.Dispose();
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

    public class ViewCellTemplateSelector : DataTemplateSelector
    {
        private DataTemplate t1;

        public ViewCellTemplateSelector(DataTemplate template1)
        {
            this.t1 = template1;
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            KFN.ResorceFile resource = (item as KFN.ResorceFile);
            if (resource.FileType == "Text" || resource.FileType == "Lyrics" || resource.FileType == "Image")
            {
                return this.t1;
            }

            DataTemplate viewColumnEmptyTemplate = new DataTemplate();
            FrameworkElementFactory viewButtonEmptyFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBox));
            viewColumnEmptyTemplate.VisualTree = viewButtonEmptyFactory;
            return viewColumnEmptyTemplate;
        }
    }

    public class LyricCellTemplateSelector : DataTemplateSelector
    {
        private DataTemplate t1;

        public LyricCellTemplateSelector(DataTemplate template1)
        {
            this.t1 = template1;
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            KFN.ResorceFile resource = (item as KFN.ResorceFile);
            if (resource.FileType == "Lyrics")
            {
                return this.t1;
            }

            DataTemplate viewColumnEmptyTemplate = new DataTemplate();
            FrameworkElementFactory viewButtonEmptyFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBox));
            viewColumnEmptyTemplate.VisualTree = viewButtonEmptyFactory;
            return viewColumnEmptyTemplate;
        }
    }
}
