using System;
using System.Collections.Generic;

using IniParser.Model;

namespace KFN_Viewer
{
    class SongINI
    {
        private List<BlockInfo> blocks = new List<BlockInfo>();
        private Dictionary<int, string> iniBlockTypes = new Dictionary<int, string> {
            {1, "Vertical text"},
            {2, "Classic karaoke"},
            {21, "Sprites"},
            {62, "Video"},
            {51, "Background"},
            {53, "MilkDrop"}
        };

        public List<BlockInfo> Blocks
        {
            get { return this.blocks; }
        }

        public SongINI(string iniText)
        {
            this.ParseINI(iniText);
        }

        private string GetIniBlockType(int id)
        {
            if (iniBlockTypes.ContainsKey(id)) { return iniBlockTypes[id]; }
            return "Unknown [" + id + "]";
        }

        public class BlockInfo
        {
            private string name;
            private string id;
            private string type;
            private string content;

            public string Name { get { return this.name; } }
            public string Id { get { return this.id; } }
            public string Type { get { return this.type; } }
            public string Content { get { return this.content; } }

            public BlockInfo(SectionData block, string KFNBlockType)
            {
                this.name = block.SectionName;
                this.id = block.Keys["ID"];
                this.type = KFNBlockType;

                string blockContent = "";
                foreach (KeyData key in block.Keys)
                {
                    blockContent += key.KeyName + "=" + key.Value + "\n";
                }
                this.content = blockContent;
            }
        }

        private void ParseINI(string iniText)
        {
            var parser = new IniParser.Parser.IniDataParser();
            IniData iniData = parser.Parse(iniText);

            foreach (SectionData block in iniData.Sections)
            {
                string blockId = block.Keys["ID"];
                this.blocks.Add(new BlockInfo(
                    block,
                    (blockId != null) ? this.GetIniBlockType(Convert.ToInt32(blockId)) : ""
                ));
            }
        }
    }
}
