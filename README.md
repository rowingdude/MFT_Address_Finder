# MFT_Address_Finder

This program is proof of concept to report the location of the master file table on an NTFS file system. 

Referencing: https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2003/cc781134(v=ws.10)#ntfs-physical-structure for areas to check.

It compiles and runs (as administrator), on my machine, reports:
    
    MFT Start LCN: 786432
    Bytes Per Sector: 512
    Sectors Per Cluster: 8
    Calculated MFT Offset: 0xC0000000
    Failed to read MFT signature. Error: 87
    Failed to verify MFT signature.
