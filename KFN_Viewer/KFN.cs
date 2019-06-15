using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO.Compression;

using Mozilla.NUniversalCharDet;
using System.Text.RegularExpressions;

public class KFN
{
    private string fileName;
    private string error;
    private Dictionary<string, string> properties = new Dictionary<string, string>();
    private List<string> unknownProperties = new List<string>();
    private List<ResorceFile> resources = new List<ResorceFile>();
    private long endOfHeaderOffset;
    // US-ASCII
    private int resourceNamesEncodingAuto = 20127;
    private string autoDetectEncoding;

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
        {1, "Config"},
        {2, "Audio"},
        {3, "Image"},
        {4, "Font"},
        {5, "Video"},
        {6, "Visualization"}
    };
    //private Dictionary<int, string> iniBlockTypes = new Dictionary<int, string> {
    //    {1, "Vertical text"},
    //    {2, "Classic karaoke"},
    //    {21, "Sprites"},
    //    {62, "Video"},
    //    {51, "Background"},
    //    {53, "MilkDrop"}
    //};

    public KFN(string fileName)
    {
        this.fileName = fileName;
        this.ReadFile();
    }

    public string isError
    {
        get { return this.error; }
    }

    public string FileName
    {
        get { return this.fileName; }
    }

    public Dictionary<string, string> Properties
    {
        get { return this.properties; }
    }

    public List<string> UnknownProperties
    {
        get { return this.unknownProperties; }
    }

    public List<ResorceFile> Resources
    {
        get { return this.resources; }
    }

    public string AutoDetectEncoding
    {
        get { return this.autoDetectEncoding; }
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

    //public string GetIniBlockType(int id)
    //{
    //    if (iniBlockTypes.ContainsKey(id)) { return iniBlockTypes[id]; }
    //    return "Unknown [" + id + "]";
    //}

    private class FileBytesAbstraction : TagLib.File.IFileAbstraction
    {
        public FileBytesAbstraction(string name, byte[] data)
        {
            Name = name;
            var stream = new MemoryStream(data);
            ReadStream = stream;
            WriteStream = stream;
        }

        public void CloseStream(Stream stream)
        {
            stream.Dispose();
        }

        public string Name { get; private set; }

        public Stream ReadStream { get; private set; }

        public Stream WriteStream { get; private set; }
    }

    public class ResorceFile
    {
        private string Type;
        private string Name;
        private int EncryptedLength;
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
        public int EncLength
        {
            get { return this.EncryptedLength; }
            //set { this.Length = value; }
        }
        public int FileLength
        {
            get { return this.Length; }
            //set { this.Length = value; }
        }
        public string FileSize
        {
            get
            {
                string[] Suffixes = { "b", "Kb", "Mb" };
                int i = 0;
                decimal dVal = (decimal)this.Length;
                while (Math.Round(dVal, 1) >= 1000)
                {
                    dVal /= 1024;
                    i++;
                }
                return String.Format("{0:n" + 1 + "} {1}", dVal, Suffixes[i]);
            }
        }
        public int FileOffset
        {
            get { return this.Offset; }
        }
        public bool IsEncrypted
        {
            get { return this.Encrypted; }
        }

        public ResorceFile(string type, string name, int enclength, int length, int offset, bool encrypted)
        {
            this.Type = type;
            this.Name = name;
            this.EncryptedLength = enclength;
            this.Length = length;
            this.Offset = offset;
            this.Encrypted = encrypted;
        }
    }

    public void ReadFile(int filesEncoding = 0)
    {
        this.error = null;
        this.properties.Clear();
        this.unknownProperties.Clear();
        this.resources.Clear();

        using (FileStream fs = new FileStream(this.fileName, FileMode.Open, FileAccess.Read))
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
                            this.unknownProperties.Add(SpropName + ": " + BitConverter.ToUInt32(propValue, 0));
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
                            this.unknownProperties.Add(SpropName + ": " + new string(Encoding.UTF8.GetChars(value)));
                        }
                        if (propName != SpropName)
                        {
                            this.properties.Add(SpropName, new string(Encoding.UTF8.GetChars(value)));
                        }
                    }
                }
                else
                {
                    this.error = "unknown property block type - " + prop[4];
                    return;
                }
                maxProps--;
            }

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

                if (filesEncoding == 0 && resourceNamesEncodingAuto == 20127)
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
                        resourceNamesEncodingAuto = denc.CodePage;
                        this.autoDetectEncoding = denc.CodePage + ": " + denc.EncodingName;
                    }
                    else if (enc == null)
                    {
                        Encoding denc = Encoding.GetEncoding(resourceNamesEncodingAuto);
                        this.autoDetectEncoding = denc.CodePage + ": " + denc.EncodingName;
                    }
                    else
                    {
                        this.autoDetectEncoding = "No supported: use " + Encoding.GetEncoding(resourceNamesEncodingAuto).EncodingName;
                    }
                }

                int useEncoding = (filesEncoding != 0) ? filesEncoding : resourceNamesEncodingAuto;
                string fName = new string(Encoding.GetEncoding(useEncoding).GetChars(resourceName));

                this.resources.Add(new KFN.ResorceFile(
                    this.GetFileType(resourceType),
                    fName,
                    BitConverter.ToInt32(resourceEncryptedLenght, 0),
                    BitConverter.ToInt32(resourceLenght, 0),
                    BitConverter.ToInt32(resourceOffset, 0),
                    (encrypted == 0) ? false : true
                ));

                resourcesCount--;
            }
            this.endOfHeaderOffset = fs.Position;
        }
    }

    public byte[] createEMZ(string iniOrElyrText, bool withVideo = false)
    {
        this.error = null;
        string audioFile = this.GetAudioSourceName();
        if (audioFile == null)
        {
            this.error = "Can`t find audio source property!";
            return null;
        }
        ResorceFile audioResource = this.Resources.Where(r => r.FileName == audioFile).FirstOrDefault();
        if (audioResource == null)
        {
            this.error = "Can`t find resource for audio source property!";
            return null;
        }

        ResorceFile videoResource = this.GetVideoResource();
        if (withVideo && videoResource == null)
        {
            this.error = "Can`t find or KFN contain more one video resource!";
            return null;
        }

        ResorceFile lyricResource = this.Resources.Where(r => r.FileName == "Song.ini").FirstOrDefault();
        if (lyricResource == null)
        {
            this.error = "Can`t find Song.ini!";
            return null;
        }

        FileInfo sourceFile = new FileInfo(audioFile);
        string elyrFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".elyr";

        string elyrText = (Regex.IsMatch(iniOrElyrText, @"[\r\n]Sync[0-9]+=[0-9,]+"))
            ? this.INIToELYR(iniOrElyrText)
            : iniOrElyrText;
        if (elyrText == null)
        {
            if (this.error == null) { this.error = "Fail to create ELYR!"; }
            return null;
        }

        byte[] bom = Encoding.Unicode.GetPreamble();
        string elyrHeader = "encore.lg-karaoke.ru ver=02 crc=00000000 \r\n";
        byte[] elyr = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, Encoding.UTF8.GetBytes(elyrHeader + elyrText));
        Array.Resize(ref bom, bom.Length + elyr.Length);
        Array.Copy(elyr, 0, bom, 2, elyr.Length);

        using (MemoryStream memStream = new MemoryStream())
        {
            //int cp = System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
            int cp = 866;
            using (ZipArchive archive = new ZipArchive(memStream, ZipArchiveMode.Create, true, Encoding.GetEncoding(cp)))
            {
                ZipArchiveEntry lyricEntry = archive.CreateEntry(elyrFileName);
                using (MemoryStream lyricBody = new MemoryStream(bom))
                using (Stream ls = lyricEntry.Open())
                {
                    lyricBody.CopyTo(ls);
                }

                ZipArchiveEntry audioEntry = archive.CreateEntry(audioResource.FileName);

                TagLib.File file = TagLib.File.Create(new FileBytesAbstraction(audioResource.FileName, this.GetDataFromResource(audioResource)));

                using (MemoryStream audioBody = new MemoryStream(this.GetDataFromResource(audioResource)))
                using (Stream aus = audioEntry.Open())
                {
                    audioBody.CopyTo(aus);
                }

                if (withVideo)
                {
                    ZipArchiveEntry videoEntry = archive.CreateEntry(videoResource.FileName);
                    using (MemoryStream videoBody = new MemoryStream(this.GetDataFromResource(videoResource)))
                    using (Stream aus = videoEntry.Open())
                    {
                        videoBody.CopyTo(aus);
                    }
                }
            }

            return memStream.ToArray();
        }
    }

    public string INIToELYR(string iniText)
    {
        this.error = null;
        Dictionary<string[], int[]> TWords = this.parseTextFromINI(iniText);
        if (TWords == null) { return null; }
        string[] words = TWords.First().Key;
        int[] timings = TWords.First().Value;

        if (words.Length == 0)
        {
            this.error = "Not found words in ini block!";
            return null;
        }
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

    public string INIToExtLRC(string iniText)
    {
        this.error = null;
        Dictionary<string[], int[]> TWords = this.parseTextFromINI(iniText);
        if (TWords == null) { return null; }
        string[] words = TWords.First().Key;
        int[] timings = TWords.First().Value;

        string lrcText = "";
        if (words.Length == 0)
        {
            this.error = "Not found words in ini block!";
            return null;
        }
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
        KeyValuePair<string, string> artistProp = this.properties.Where(kv => kv.Key == "Artist").FirstOrDefault();
        KeyValuePair<string, string> titleProp = this.properties.Where(kv => kv.Key == "Title").FirstOrDefault();
        if (titleProp.Value != null) { lrcText = "[ti:" + titleProp.Value + "]\n" + lrcText; }
        if (artistProp.Value != null) { lrcText = "[ar:" + artistProp.Value + "]\n" + lrcText; }

        return lrcText;
    }

    private Dictionary<string[], int[]> parseTextFromINI(string iniBlock)
    {
        this.error = null;
        Regex textRegex = new Regex(@"^Text[0-9]+=(.+)");
        Regex syncRegex = new Regex(@"^Sync[0-9]+=([0-9,]+)");
        string[] words = { };
        int[] timings = { };
        int lines = 0;
        // remove double spaces
        string iniText = iniBlock.Replace("  ", " ");
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
            this.error = "Fail convert: words - " + words.Length + ", timings - " + timings.Length;
            return null;
        }

        Dictionary<string[], int[]> TWords = new Dictionary<string[], int[]>();
        TWords.Add(words, timings);
        return TWords;
    }

    public string GetAudioSourceName()
    {
        if (this.properties.Count == 0) { return null; }
        //1,I,ddt_-_chto_takoe_osen'.mp3
        KeyValuePair<string, string> sourceProp = this.properties.Where(kv => kv.Key == "Source").FirstOrDefault();
        return sourceProp.Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Last();
    }

    public ResorceFile GetVideoResource()
    {
        // kfn may contain more then one video resource
        ResorceFile[] videos = this.resources.Where(r => r.FileType == "Video").ToArray();
        return (videos.Length == 1) ? videos[0] : null;
    }

    public byte[] GetDataFromResource(ResorceFile resource)
    {
        byte[] data = new byte[resource.EncLength];
        using (FileStream fs = new FileStream(this.fileName, FileMode.Open, FileAccess.Read))
        {
            fs.Position = this.endOfHeaderOffset + resource.FileOffset;
            fs.Read(data, 0, data.Length);
        }

        if (resource.IsEncrypted)
        {
            byte[] Key = Enumerable.Range(0, this.properties["AES-ECB-128 Key"].Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(this.properties["AES-ECB-128 Key"].Substring(x, 2), 16))
                .ToArray();
            data = DecryptData(data, Key);
            // delete end garbage
            Array.Resize(ref data, resource.FileLength);
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
}
