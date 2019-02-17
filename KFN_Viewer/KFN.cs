using System;
using System.Collections.Generic;

public class KFN
{
    private Dictionary<string, string> blocksDesc = new Dictionary<string, string>{
        {"DIFM", "DIFM"},
        {"DIFW", "DIFW"},
        {"GNRE", "Genre"},
        {"SFTV", "SFTV"},
        {"MUSL", "MUSL"},
        {"ANME", "ANME"},
        {"TYPE", "TYPE"},
        {"FLID", "FLID"},
        {"TITL", "Title"},
        {"ARTS", "Artist"},
        {"ALBM", "Album"},
        {"COMP", "COMP"},
        {"SORC", "Source"},
        {"TRAK", "Track number"},
        {"RGHT", "RGHT"},
        {"PROV", "PROV"},
        {"IDUS", "IDUS"},
        {"LANG", "Language"}
    };
    private Dictionary<int, string> fileTypes = new Dictionary<int, string> {
        {1, "Lyrics"},
        {2, "Audio"},
        {3, "Image"},
        {4, "Font"},
        {5, "Video"}
    };

    public KFN() {}

    public string GetBlockDesc(string BlockName)
    {
        if (blocksDesc.ContainsKey(BlockName)) { return blocksDesc[BlockName]; }
        return BlockName + " (unknown)";
    }

    public string GetFileType(byte[] type)
    {
        int ftype = BitConverter.ToInt32(type, 0);
        if (fileTypes.ContainsKey(ftype)) { return fileTypes[ftype]; }
        return "Unknown (" + ftype + ")";
    }
}
