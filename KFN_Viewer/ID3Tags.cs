using System.IO;

namespace KFN_Viewer
{
    class ID3Tags
    {
        public byte[] ModTags(byte[] data)
        {
            Stream dataStream = new MemoryStream(data);
            TagLib.File tagData = TagLib.File.Create(new FileBytesAbstraction("tempFile.mp3", dataStream));
            //TagLib.Tag tempTags = new TagLib.Id3v2.Tag();
            //tagData.Tag.Lyrics = "test lyric";
            //tagData.RemoveTags(TagLib.TagTypes.AllTags);
            //tagData.Tag.CopyTo(tempTags, true);
            tagData.Save();
            tagData.Dispose();
            //tagData = TagLib.File.Create(new FileBytesAbstraction(audioFile, dataStream));
            //tempTags.CopyTo(tagData.Tag, true);
            //tagData.Save();
            //tagData.Dispose();

            byte[] dStream = new byte[dataStream.Length];
            dataStream.Read(dStream, 0, dStream.Length);
            return dStream;
        }

        private class FileBytesAbstraction : TagLib.File.IFileAbstraction
        {
            public FileBytesAbstraction(string name, Stream data)
            {
                Name = name;
                ReadStream = data;
                WriteStream = data;
            }
            
            public void CloseStream(Stream stream)
            {
                stream.Position = 0;
            }

            public string Name { get; private set; }

            public Stream ReadStream { get; private set; }

            public Stream WriteStream { get; private set; }
        }
    }
}
