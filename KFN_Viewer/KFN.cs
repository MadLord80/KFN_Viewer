using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.IO.Compression;

using Mozilla.NUniversalCharDet;

public class KFN
{
    private string fullFileName;
    private string error;
    private Dictionary<string, string> properties = new Dictionary<string, string>();
    private List<string> unknownProperties = new List<string>();
    private List<ResourceFile> resources = new List<ResourceFile>();
    private long endOfHeaderOffset;
    private long endOfPropsOffset;
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

    public KFN(string fileName)
    {
        this.fullFileName = fileName;
        this.ReadFile();
    }

    public string isError
    {
        get { return this.error; }
    }

    public string FullFileName
    {
        get { return this.fullFileName; }
    }

    public Dictionary<string, string> Properties
    {
        get { return this.properties; }
    }

    public List<string> UnknownProperties
    {
        get { return this.unknownProperties; }
    }

    public List<ResourceFile> Resources
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

    private int GetFileTypeId(string type)
    {
        KeyValuePair<int, string> ftype = this.fileTypes.Where(ft => ft.Value == type).FirstOrDefault(); 
        return (ftype.Value == null) ? -1 : ftype.Key;
    }

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

    public class ResourceFile
    {
        private string Type;
        private string Name;
        private int EncryptedLength;
        private int Length;
        private int Offset;
        private bool Encrypted;

        private bool Exported;
        private bool IsAudioSource;

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
        }
        public int FileLength
        {
            get { return this.Length; }
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
        public bool IsExported
        {
            get {
                return (this.FileType == "Config" || this.IsAudioSource) ? true : this.Exported;
            }
            set {
                if (this.FileType != "Config" && !this.IsAudioSource) { this.Exported = value; }
            }
        }

        public ResourceFile(string type, string name, int enclength, int length, int offset, bool encrypted, bool aSource = false)
        {
            this.Type = type;
            this.Name = name;
            this.EncryptedLength = enclength;
            this.Length = length;
            this.Offset = offset;
            this.Encrypted = encrypted;
            this.IsAudioSource = aSource;
        }
    }

    public void ReadFile(int filesEncoding = 0)
    {
        this.error = null;
        this.properties.Clear();
        this.unknownProperties.Clear();
        this.resources.Clear();

        using (FileStream fs = new FileStream(this.fullFileName, FileMode.Open, FileAccess.Read))
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
            this.endOfPropsOffset = fs.Position;

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

                this.resources.Add(new KFN.ResourceFile(
                    this.GetFileType(resourceType),
                    fName,
                    BitConverter.ToInt32(resourceEncryptedLenght, 0),
                    BitConverter.ToInt32(resourceLenght, 0),
                    BitConverter.ToInt32(resourceOffset, 0),
                    (encrypted == 0) ? false : true,
                    (fName == this.GetAudioSourceName()) ? true : false
                ));

                resourcesCount--;
            }
            this.endOfHeaderOffset = fs.Position;
        }
    }

    public byte[] createEMZ(string iniOrElyrText, bool withVideo = false, ResourceFile video = null, ResourceFile audio = null)
    {
        this.error = null;
        string audioFile = (audio != null) ? audio.FileName : this.GetAudioSourceName();
        if (audioFile == null)
        {
            this.error = "Can`t find audio source property!";
            return null;
        }
        ResourceFile audioResource = (audio != null)
            ? audio
            : this.Resources.Where(r => r.FileName == audioFile).FirstOrDefault();
        if (audioResource == null)
        {
            this.error = "Can`t find resource for audio source property!";
            return null;
        }

        ResourceFile videoResource = (video != null) ? video : this.GetVideoResource();
        if (withVideo && videoResource == null)
        {
            this.error = "Can`t find or KFN contain more one video resource!";
            return null;
        }

        FileInfo sourceFile = new FileInfo(audioFile);
        string elyrFileName = sourceFile.Name.Substring(0, sourceFile.Name.Length - sourceFile.Extension.Length) + ".elyr";

        string elyrText = (Regex.IsMatch(iniOrElyrText, @"[\r\n][Ss]ync[0-9]+=[0-9,]+"))
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
            if (i == words.Length - 1 && (words[i] == "_" || words[i] == null || words[i] == "")) { break; }
            if (!newLine)
            {
                if (words[i] == "" && timeIndex < timings.Length - 1)
                {
                    timeIndex++;
                    words[i] = null;
                }

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
        string pattern = @"^[0-9]+:[0-9]+=\\_[\r\n]+";
        elyrText = Regex.Replace(elyrText, pattern, "", RegexOptions.Multiline);
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

            if (words[i] != null && words[i].Length == 1 && words[i] == "_")
            {
                timeIndex++;
                lrcText += "\n";
                newLine = true;
                continue;
            }

            // in end of line: +45 msec
            int timing = (words[i] != null) ? timings[timeIndex] : timings[timeIndex - 1] + 45;
            decimal time = Convert.ToDecimal(timing);
            decimal min = Math.Truncate(time / 6000);
            decimal sec = Math.Truncate((time - min * 6000) / 100);
            decimal msec = Math.Truncate(time - (min * 6000 + sec * 100));

            lrcText += startTag + String.Format("{0:D2}", (int)min) + ":"
                    + String.Format("{0:D2}", (int)sec) + "."
                    + String.Format("{0:D2}", (int)msec) + endTag;

            if (words[i] != null && words[i] != "")
            {
                lrcText += words[i];
                newLine = false;
                timeIndex++;
            }
            else
            {
                if (words[i] == "") { timeIndex++; }
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

    public string INItoUltraStar(string iniText, decimal BPM)
    {
        this.error = null;
        List<KeyValuePair<decimal, string>> TWords = this.parseTextFromINI_new(iniText);
        if (TWords.Count == 0) { return null; }
        
        string usText = "";

        decimal GAP = TWords[0].Key * 10;

        //: Regular note
        //* Golden note
        //F Freestyle syllable
        //– Line break (separates lyrics into suitable lines).
        
        //: 7 6 0 Поб
        //: 13 7 1 лед
        //: 20 8 1 нев
        //: 28 7 - 2 ши
        //: 35 6 - 2 е
        //: 54 8 0  ли
        //: 62 11 - 2 стья
        //: 75 6 2  о
        //: 81 8 2 кна
        //- 113
        //: 146 8 1 За
        //: 154 4 0 ра
        //: 158 12 2 ста
        //: 170 8 2 ют
        //: 187 10 - 6  проз
        //: 197 14 - 5 рач
        //: 211 6 - 6 ной
        //: 224 6 - 8  во
        //: 230 10 0 дой
        //- 250
        //The first value means the time stamp which indicates when the tone begins which is to be sung.
        //The second value indicates the length of this tone in beats.If the following tone does not begin right after the previous one, 
        //you have breaks. In our example: there is a break lasting for 1 beat after "always", "talk", "a" and "things".
        //The third value indicates the pitch of the tone(0 = C1.) After that you see the syllable which belongs to the tone.
        //UltraStar determines if the following syllable still belongs to one word by blanks:
        //if there is NO blank after the syllable, the next syllable still belongs to the word;
        //if there is a blank after the syllable, the next syllable will be classified as the beginning of a new word.
        //The line
        //- 152
        //indicates that everything following it shall be shown in a new line on the screen.
        //It can also look like this:
        //-152 153
        //Here, the first value indicates the time when the previous line shall disappear from the screen 
        //and the second value indicates when the following line shall appear.

        //ms = bpm / (BPM * 4) * 60000 + GAP
        //bpm = (ms - GAP) / 60000 * (BPM * 4)
        //2610 / 60000 * 1491.4 = 64,88
        //372.85 * 4 = 1491,4
        //? / 1491.4 * 60000 + 34960 = 37030
        decimal oneBpmInMs = 60000 / (BPM * 4);

        // null - end of line (no time code)
        // '' - empty line (no time code, skipped in lrc)
        // '_' - empty line (has time code, skipped in lrc)

        //bool newLine = true;
        //[6] 2278 : It'll 
        //[7] 2301 : leave
        //[8] 2325 : you
        //[9] 2348 : blind
        //[10] 2396 : $
        //[11] 0 : 
        //[12] 3056 : What
        //[13] 3080 : we
        //[14] 3103 : have
        //[15] 3132 : made
        //[16] 0 : 

        //[0] 3496 : В
        //[1] 3530 : по
        //[2] 3567 : блед
        //[3] 3599 : нев
        //[4] 3619 : ших
        //[5] 3703 : лис
        //[6] 3757 : тьях
        //[7] 3809 : ок
        //[8] 3843 : нa
        //[9] 0 : 
        //[10] 4045 : Зa
        //[11] 4082 : pас
        //[12] 4116 : тa
        //[13] 4169 : ет
        //[14] 4219 : пpо
        //[15] 4250 : зpaч
        //[16] 4303 : ной
        //[17] 4353 : во
        //[18] 4392 : дой
        //[19] 0 : 
        //: 7 6 0 Поб
        //: 13 7 1 лед
        //: 20 8 1 нев
        //: 28 7 - 2 ши
        //: 35 6 - 2 е
        //: 54 8 0  ли
        //: 62 11 - 2 стья
        //: 75 6 2  о
        //: 81 8 2 кна
        //- 113
        //: 146 8 1 За
        //: 154 4 0 ра
        //: 158 12 2 ста
        //: 170 8 2 ют
        //: 187 10 - 6  проз
        //: 197 14 - 5 рач
        //: 211 6 - 6 ной
        //: 224 6 - 8  во
        //: 230 10 0 дой
        //- 250
        for (int i = 0; i < TWords.Count; i++)
        {
            KeyValuePair<decimal, string> kv = TWords[i];

            decimal time = Convert.ToDecimal(kv.Key * 10);
            
            if (kv.Value == null)
            {
                // if some previous also == null ???
                decimal lineEndsDuration = (i + 1 == TWords.Count) ? 20 : Math.Floor((TWords[i + 1].Key * 10 - TWords[i - 1].Key * 10) / 2);
                time = (TWords[i - 1].Value == "$") ? TWords[i - 1].Key * 10 : TWords[i - 1].Key * 10 + lineEndsDuration;
                decimal bpm = Math.Floor(this.ms2bpm(time, GAP, BPM));
                usText += "- " + bpm + "\n";
            }
            else if (kv.Value == "$")
            {
                continue;
            }
            else
            {
                decimal bpm = Math.Floor(this.ms2bpm(time, GAP, BPM));
                decimal wordDuration = 20;
                if (i + 1 < TWords.Count && TWords[i + 1].Key == 0)
                {
                    wordDuration = 1;
                }
                else if (i + 1 < TWords.Count)
                {
                    wordDuration = (TWords[i + 1].Key * 10 - time) / oneBpmInMs;
                }
                usText += ": " + bpm + " " + Math.Floor(wordDuration) + " 0 " + kv.Value + "\n";
            }
        }

        usText = "#RELATIVE:no\n" + usText;
        usText = "#GAP:" + GAP + "\n" + usText;
        List<ResourceFile> videos = this.resources.Where(r => r.FileType == "Video").ToList();
        string video = (videos.Count == 0 || videos.Count > 1) ? "" : videos[0].FileName;
        usText = "#VIDEO:" + video + "\n" + usText;
        usText = "#MP3:" + this.GetAudioSourceName() + "\n" + usText;
        KeyValuePair<string, string> artistProp = this.properties.Where(kv => kv.Key == "Artist").FirstOrDefault();
        KeyValuePair<string, string> titleProp = this.properties.Where(kv => kv.Key == "Title").FirstOrDefault();
        string artist = artistProp.Value ?? "";
        usText = "#ARTIST:" + artist + "\n" + usText;
        string title = titleProp.Value ?? "";
        usText = "#TITLE:" + title + "\n" + usText;

        usText += "E\n";
         
        return usText;
    }

    private decimal ms2bpm(decimal ms, decimal gap, decimal BPM)
    {        
        decimal bpm = (ms - gap) / 60000 * (BPM * 4);
        return bpm;
    }

    private Dictionary<string[], int[]> parseTextFromINI(string iniBlock)
    {
        this.error = null;
        Regex textRegex = new Regex(@"^[Tt]ext[0-9]+=(.+)");
        Regex syncRegex = new Regex(@"^[Ss]ync[0-9]+=([0-9,]+)");
        string[] words = { };
        int[] timings = { };
        int lines = 0;
        
        // remove double spaces
        string iniText = iniBlock.Replace("  ", " ");
        // example 1: Se/a/son tic/ket on
        // example 2: Cos I'm/ your su/per/-he/ro/_
        // '/' and ' ' is delimiter, '/_\n' is end of line (has time code), '\n' is end of line (no time code)
        // '/ ' - is delimiter (end of word), has time code
        // '' - empty line (no time code, skipped in lrc)
        // '_' - empty line (has time code, skipped in lrc)
        // time for end of line w/o time code = last time + 45 msec
        // time for end of line w/ time code = end of line time code

        foreach (string str in iniText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            Match texts = textRegex.Match(str);
            Match syncs = syncRegex.Match(str);
            if (texts.Groups.Count > 1)
            {
                string textLine = texts.Groups[1].Value;
                bool endLineWithTimeCode = false;
                //"/_" is end of line(has time code)
                if (textLine.Length > 1 && textLine[textLine.Length - 2] == '/' && textLine[textLine.Length - 1] == '_')
                {
                    textLine = textLine.Substring(0, textLine.Length - 2);
                    endLineWithTimeCode = true;
                }
                //"se/a/son tic/ket" => "se/a/son /tic/ket"
                textLine = textLine.Replace(" ", " /");
                string[] linewords = textLine.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                // + end of line - null (besides "_")
                //"_" - empty line(has time code)
                int endLine = (textLine.Length == 1 && textLine[0] == '_') ? 0 : 1;
                Array.Resize(ref words, words.Length + linewords.Length + endLine);
                Array.Copy(linewords, 0, words, words.Length - linewords.Length - endLine, linewords.Length);
                //end of line w/ time code: null => ""
                if (endLineWithTimeCode) { words[words.Length - 1] = ""; }
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

        // бывает, что не весь текст имеет временные метки
        // поэтому отсекаем текст в конце за пределами временных меток
        if (timings.Length < words.Length - lines)
        { 
            int cutLines = 0; int cWords = 0;
            for (int i = 0; i < words.Length; i++)
            {
                if (cWords > timings.Length)
                {
                    break;
                }
                else if (words[i] == null)
                {
                    cutLines++;
                }
                else
                {
                    cWords++;
                }
            }
            int cutIndex = timings.Length + cutLines;
            Array.Resize(ref words, cutIndex);
        }

        Dictionary<string[], int[]> TWords = new Dictionary<string[], int[]>();
        TWords.Add(words, timings);
        return TWords;
    }

    private List<KeyValuePair<decimal, string>> parseTextFromINI_new(string iniBlock)
    {
        this.error = null;
        Regex textRegex = new Regex(@"^[Tt]ext[0-9]+=(.+)");
        Regex syncRegex = new Regex(@"^[Ss]ync[0-9]+=([0-9,]+)");

        List<KeyValuePair<decimal, string>> ldata = new List<KeyValuePair<decimal, string>>();
        int timingsCurPos = 0;
        int wordsCurPos = 0;

        // remove double spaces
        string iniText = iniBlock.Replace("  ", " ");
        // example 1: Se/a/son tic/ket on
        // example 2: Cos I'm/ your su/per/-he/ro/_
        // '/' and ' ' is delimiter, '/_\n' is end of line (has time code), '\n' is end of line (no time code)
        // '/ ' - is delimiter (end of word), has time code
        // '' - empty line (no time code, skipped in lrc)
        // '_' - empty line (has time code, skipped in lrc)
        // time for end of line w/o time code = last time + 45 msec
        // time for end of line w/ time code = end of line time code

        foreach (string str in iniText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            Match texts = textRegex.Match(str);
            Match syncs = syncRegex.Match(str);
            if (texts.Groups.Count > 1)
            {
                string textLine = texts.Groups[1].Value;
                bool endLineWithTimeCode = false;
                //"/_" is end of line(has time code)
                if (textLine.Length > 1 && textLine[textLine.Length - 2] == '/' && textLine[textLine.Length - 1] == '_')
                {
                    textLine = textLine.Substring(0, textLine.Length - 2);
                    endLineWithTimeCode = true;
                }
                //"se/a/son tic/ket" => "se/a/son /tic/ket"
                textLine = textLine.Replace(" ", " /");
                string[] linewords = textLine.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in linewords)
                {
                    if (ldata.Count > wordsCurPos)
                    {
                        decimal time = ldata[wordsCurPos].Key;
                        ldata[wordsCurPos++] = new KeyValuePair<decimal, string>(time, word);
                    }
                    else
                    {
                        ldata.Add(new KeyValuePair<decimal, string>(-1, word));
                    }
                }
                if (endLineWithTimeCode)
                {
                    if (ldata.Count > wordsCurPos)
                    {
                        decimal time = ldata[wordsCurPos].Key;
                        ldata[wordsCurPos++] = new KeyValuePair<decimal, string>(time, "$");
                    }
                    else
                    {
                        ldata.Add(new KeyValuePair<decimal, string>(-1, "$"));
                    }
                }
                //end of line - null
                if (ldata.Count > wordsCurPos)
                {
                    ldata.Insert(wordsCurPos++, new KeyValuePair<decimal, string>(0, null));
                }
                else
                {
                    ldata.Add(new KeyValuePair<decimal, string>(0, null));
                }
            }
            else if (syncs.Groups.Count > 1)
            {
                string songLine = syncs.Groups[1].Value;
                int[] linetimes = songLine.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.Parse(s)).ToArray();
                foreach (int time in linetimes)
                {
                    if (ldata.Count > timingsCurPos)
                    {
                        ldata[timingsCurPos] = new KeyValuePair<decimal, string>((decimal)time, ldata[timingsCurPos].Value);
                    }
                    else
                    {
                        ldata.Add(new KeyValuePair<decimal, string>((decimal)time, "@"));
                    }
                    timingsCurPos++;
                }
            }
        }

        return ldata;
    }

    public void ChangeKFN(List<ResourceFile> resources, bool needDecrypt = false)
    {
        this.error = null;

        string sourceKFNFile = this.fullFileName + ".bak";
        File.Copy(this.fullFileName, sourceKFNFile);

        byte[] sourcePropHeader = new byte[this.endOfPropsOffset];
        using (FileStream fs = new FileStream(sourceKFNFile, FileMode.Open, FileAccess.ReadWrite))
        {
            fs.Read(sourcePropHeader, 0, sourcePropHeader.Length);
        }

        File.Delete(this.fullFileName);
        using (FileStream newFile = new FileStream(this.fullFileName, FileMode.Create, FileAccess.ReadWrite))
        {
            newFile.Write(sourcePropHeader, 0, sourcePropHeader.Length);

            if (needDecrypt)
            {
                newFile.Position = 4;

                byte[] prop = new byte[5];
                byte[] propValue = new byte[4];
                int maxProps = 40;
                while (maxProps > 0)
                {
                    newFile.Read(prop, 0, prop.Length);
                    string propName = new string(Encoding.UTF8.GetChars(new ArraySegment<byte>(prop, 0, 4).ToArray()));
                    if (propName == "ENDH")
                    {
                        newFile.Position += 4;
                        break;
                    }
                    else if (propName == "FLID")
                    {
                        newFile.Read(propValue, 0, propValue.Length);
                        uint valueLength = BitConverter.ToUInt32(propValue, 0);
                        byte[] zeroValue = new byte[valueLength];
                        newFile.Write(zeroValue, 0, zeroValue.Length);

                        maxProps--;
                        continue;
                    }
                    else if (propName == "RGHT")
                    {
                        byte[] zeroValue = new byte[4];
                        newFile.Write(zeroValue, 0, zeroValue.Length);

                        maxProps--;
                        continue;
                    }
                    if (prop[4] == 1)
                    {
                        newFile.Position += 4;
                    }
                    else if (prop[4] == 2)
                    {
                        newFile.Read(propValue, 0, propValue.Length);
                        newFile.Position += BitConverter.ToInt32(propValue, 0);
                    }
                    maxProps--;
                }
            }

            byte[] numOfResources = BitConverter.GetBytes(resources.Count);
            newFile.Write(numOfResources, 0, numOfResources.Length);
            int nOffset = 0;
            foreach (ResourceFile resource in resources.OrderBy(r => r.FileOffset))
            {
                byte[] resourceNameLenght = BitConverter.GetBytes(resource.FileName.Length);
                byte[] resourceLenght = BitConverter.GetBytes(resource.FileLength);
                byte[] resourceEncryptedLenght = (needDecrypt)
                    ? resourceLenght
                    : BitConverter.GetBytes(resource.EncLength);
                int encrypted = (resource.IsEncrypted) ? 1 : 0;
                byte[] resourceEncrypted = (needDecrypt)
                    ? new byte[4]
                    : BitConverter.GetBytes(encrypted);

                newFile.Write(resourceNameLenght, 0, resourceNameLenght.Length);
                byte[] resourceName = Encoding.GetEncoding(this.resourceNamesEncodingAuto).GetBytes(resource.FileName);
                newFile.Write(resourceName, 0, resourceName.Length);
                byte[] type = BitConverter.GetBytes(this.GetFileTypeId(resource.FileType));
                newFile.Write(type, 0, type.Length);
                newFile.Write(resourceLenght, 0, resourceLenght.Length);
                byte[] rOffset = BitConverter.GetBytes(nOffset);
                newFile.Write(rOffset, 0, rOffset.Length);
                nOffset += (needDecrypt) ? resource.FileLength : resource.EncLength;
                newFile.Write(resourceEncryptedLenght, 0, resourceEncryptedLenght.Length);
                newFile.Write(resourceEncrypted, 0, resourceEncrypted.Length);
            }

            KFN sourceKFN = new KFN(sourceKFNFile);
            foreach (ResourceFile resource in resources.OrderBy(r => r.FileOffset))
            {
                byte[] rData = sourceKFN.GetDataFromResource(resource, needDecrypt);
                newFile.Write(rData, 0, rData.Length);
            }
        }
    }

    public string GetAudioSourceName()
    {
        if (this.properties.Count == 0) { return null; }
        //1,I,ddt_-_chto_takoe_osen'.mp3
        KeyValuePair<string, string> sourceProp = this.properties.Where(kv => kv.Key == "Source").FirstOrDefault();
        return sourceProp.Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Last();
    }

    public ResourceFile GetVideoResource()
    {
        // kfn may contain more then one video resource
        ResourceFile[] videos = this.resources.Where(r => r.FileType == "Video").ToArray();
        return (videos.Length == 1) ? videos[0] : null;
    }

    public byte[] GetDataFromResource(ResourceFile resource, bool needDecrypt = true)
    {
        byte[] data = new byte[resource.EncLength];
        using (FileStream fs = new FileStream(this.fullFileName, FileMode.Open, FileAccess.Read))
        {
            fs.Position = this.endOfHeaderOffset + resource.FileOffset;
            fs.Read(data, 0, data.Length);
        }

        if (resource.IsEncrypted && needDecrypt)
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
