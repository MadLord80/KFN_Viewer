using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class KFN
{
    private string error;
    private Dictionary<string, string> properties = new Dictionary<string, string>();

    private Dictionary<string, string> propsDesc = new Dictionary<string, string>{
        {"DIFM", "Man difficult"},
        {"DIFW", "Woman difficult"},
        {"GNRE", "Genre"},
        {"SFTV", "SFTV"},
        {"MUSL", "MUSL"},
        {"ANME", "ANME"},
        {"TYPE", "TYPE"},
        {"FLID", "AES-ECB-128 Key"},
        {"TITL", "Title"},
        {"ARTS", "Artist"},
        {"ALBM", "Album"},
        {"COMP", "Composer"},
        {"SORC", "Source"},
        {"TRAK", "Track number"},
        {"RGHT", "RGHT"},
        {"COPY", "Copyright"},
        {"COMM", "Comment"},
        {"PROV", "PROV"},
        {"IDUS", "IDUS"},
        {"LANG", "Language"},
        {"KFNZ", "KFN Author"},
        {"YEAR", "Year"},
        {"KARV", "Karaoke version"},
        {"VOCG", "Lead vocal"}
    };
    private Dictionary<int, string> fileTypes = new Dictionary<int, string> {
        {0, "Text"},
        {1, "Lyrics"},
        {2, "Audio"},
        {3, "Image"},
        {4, "Font"},
        {5, "Video"},
        {6, "Visualization"}
    };
    private Dictionary<int, string> iniBlockTypes = new Dictionary<int, string> {
        {1, "Vertical text"},
        {2, "Classic karaoke"},
        {21, "Sprites"},
        {62, "Video"},
        {51, "Background"},
        {53, "MilkDrop"}
    };

    public KFN(string fileName)
    {       
        this.ReadFile(fileName);
    }

    public string isError()
    {
        return this.error;
    }

    public string GetPropDesc(string PropName)
    {
        if (propsDesc.ContainsKey(PropName)) { return propsDesc[PropName]; }
        return "(unknown) " + PropName;
    }

    public string GetFileType(byte[] type)
    {
        int ftype = BitConverter.ToInt32(type, 0);
        if (fileTypes.ContainsKey(ftype)) { return fileTypes[ftype]; }
        return "Unknown (" + ftype + ")";
    }

    public string GetIniBlockType(int id)
    {
        if (iniBlockTypes.ContainsKey(id)) { return iniBlockTypes[id]; }
        return "Unknown [" + id + "]";
    }

    public class ResorceFile
    {
        private string Type;
        private string Name;
        private int Length;
        private int Offset;
        private bool Encrypted;

        public string FileType
        {
            get { return this.Type; }
        }
        public string FileName
        {
            get { return this.Name; }
        }
        public int FileLength
        {
            get { return this.Length; }
            set { this.Length = value; }
        }
        public int FileOffset
        {
            get { return this.Offset; }
        }
        public bool IsEncrypted
        {
            get { return this.Encrypted; }
        }

        public ResorceFile(string type, string name, int length, int offset, bool encrypted)
        {
            this.Type = type;
            this.Name = name;
            this.Length = length;
            this.Offset = offset;
            this.Encrypted = encrypted;
        }
    }

    //private void ReadFile(int filesEncoding = 0)
    private void ReadFile(string fileName)
    {
        this.error = null;
        //propertiesView.ItemsSource = null;
        //properties.Clear();
        //resourcesView.ItemsSource = null;
        //resources.Clear();
        //ToEMZButton.IsEnabled = false;

        //fileNameLabel.Content = "KFN file: " + KFNFile;

        using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
            byte[] signature = new byte[4];
            fs.Read(signature, 0, signature.Length);
            string sign = new string(Encoding.UTF8.GetChars(signature));
            if (sign != "KFNB")
            {
                this.error = "Invalid KFN signature!";
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
                string SpropName = this.GetPropDesc(propName);
                if (prop[4] == 1)
                {
                    fs.Read(propValue, 0, propValue.Length);
                    if (SpropName == "Genre" && BitConverter.ToUInt32(propValue, 0) == 0xffffffff)
                    {
                        this.properties.Add(SpropName, "Not set");
                    }
                    else
                    {
                        if (SpropName.Contains("unknown"))
                        {
                            //PropertyWindow.Text += SpropName + ": " + BitConverter.ToUInt32(propValue, 0) + "\n";
                        }
                        if (propName != SpropName)
                        {
                            this.properties.Add(SpropName, BitConverter.ToUInt32(propValue, 0).ToString());
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
                        this.properties.Add(SpropName, val);
                    }
                    else
                    {
                        if (SpropName.Contains("unknown"))
                        {
                            //PropertyWindow.Text += SpropName + ": " + new string(Encoding.UTF8.GetChars(value)) + "\n";
                        }
                        if (propName != SpropName)
                        {
                            this.properties.Add(SpropName, new string(Encoding.UTF8.GetChars(value)));
                        }
                    }
                }
                else
                {
                    //PropertyWindow.Text += SpropName + ": unknown block type - " + prop[4] + "!\n";
                    this.properties.Add(SpropName, "unknown block type - " + prop[4]);
                    return;
                }
                maxProps--;
            }
            //propertiesView.ItemsSource = properties;

            byte[] numOfResources = new byte[4];
            fs.Read(numOfResources, 0, numOfResources.Length);
            int resourcesCount = BitConverter.ToInt32(numOfResources, 0);
            while (resourcesCount > 0)
            {
                byte[] resourceNameLenght = new byte[4];
                byte[] resourceType = new byte[4];
                byte[] resourceLenght = new byte[4];
                byte[] resourceEncryptedLenght = new byte[4];
                byte[] resourceOffset = new byte[4];
                byte[] resourceEncrypted = new byte[4];

                fs.Read(resourceNameLenght, 0, resourceNameLenght.Length);
                byte[] resourceName = new byte[BitConverter.ToUInt32(resourceNameLenght, 0)];
                fs.Read(resourceName, 0, resourceName.Length);
                fs.Read(resourceType, 0, resourceType.Length);
                fs.Read(resourceLenght, 0, resourceLenght.Length);
                fs.Read(resourceOffset, 0, resourceOffset.Length);
                fs.Read(resourceEncryptedLenght, 0, resourceEncryptedLenght.Length);
                fs.Read(resourceEncrypted, 0, resourceEncrypted.Length);
                int encrypted = BitConverter.ToInt32(resourceEncrypted, 0);

                if (filesEncoding == 0 && filesEncodingAuto == 20127)
                {
                    UniversalDetector Det = new UniversalDetector(null);
                    Det.HandleData(resourceName, 0, resourceName.Length);
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
                string fName = new string(Encoding.GetEncoding(useEncoding).GetChars(resourceName));

                resources.Add(new KFN.ResorceFile(
                    KFN.GetFileType(resourceType),
                    fName,
                    BitConverter.ToInt32(resourceEncryptedLenght, 0),
                    BitConverter.ToInt32(resourceOffset, 0),
                    (encrypted == 0) ? false : true
                ));

                resourcesCount--;
            }
            endOfHeaderOffset = fs.Position;
            resourcesView.ItemsSource = resources;
            AutoSizeColumns(resourcesView.View as GridView);
        }
        FilesEncodingElement.IsEnabled = true;
        if (resources.Count > 1) { ExportAllButton.IsEnabled = true; }

        string sourceName = GetAudioSource();
        if (sourceName != null)
        {
            KFN.ResorceFile audioResource = resources.Where(r => r.FileName == sourceName).FirstOrDefault();
            KFN.ResorceFile lyricResource = resources.Where(r => r.FileName == "Song.ini").FirstOrDefault();
            if (audioResource != null && lyricResource != null) { ToEMZButton.IsEnabled = true; }
        }
    }
}
