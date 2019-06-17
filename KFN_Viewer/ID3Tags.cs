using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFN_Viewer
{
    class ID3Tags
    {
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
