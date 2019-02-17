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
        {"IDUS", "IDUS"}
    };

    public KFN()
	{
	}

    public string GetBlockDesc(string BlockName)
    {
        if (blocksDesc.ContainsKey(BlockName)) { return blocksDesc[BlockName]; }
        return BlockName + " (unknown)";
    }
}
