using System;
using System.IO;
using System.Linq;

class ID3Tags
{
    public byte[] RemoveAllTags(byte[] data)
    {
        Stream dataStream = new MemoryStream(data);
        TagLib.File tagData = TagLib.File.Create(new FileBytesAbstraction("tempFile.mp3", dataStream));
        //TagLib.Tag tempTags = new TagLib.Id3v2.Tag();
        //tagData.Tag.Lyrics = "test lyric";
        tagData.RemoveTags(TagLib.TagTypes.AllTags);
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

    public string[] GetArtistAndTitle(byte[] data)
    {
        Stream dataStream = new MemoryStream(data);
        TagLib.File tagData = TagLib.File.Create(new FileBytesAbstraction("tempFile.mp3", dataStream));
        string artist = (tagData.Tag.Performers.Length > 0) ? toUTF8(tagData.Tag.Performers[0]) : null;
        string title = (tagData.Tag.Title != null && tagData.Tag.Title.Length > 0) ? toUTF8(tagData.Tag.Title) : null;

        return new string[] { artist, title };
    }

    private string toUTF8(string text)
    {
        if (text == null || text.Length == 0) return "";
        return new string(text.ToCharArray().
            Select(x => ((x + 848) >= 'А' && (x + 848) <= 'ё') ? (char)(x + 848) : x).
            ToArray()
        );
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
