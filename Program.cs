using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

class Program
{
    const uint GENERIC_READ = 0x80000000;
    const uint FILE_SHARE_READ = 0x00000001;
    const uint FILE_SHARE_WRITE = 0x00000002;
    const uint OPEN_EXISTING = 3;
    const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct NTFS_BOOT_SECTOR
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Jump;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] OemID;
        public ushort BytesPerSector;
        public byte SectorsPerCluster;
        public ushort ReservedSectors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Always0;
        public ushort NotUsed;
        public byte MediaDescriptor;
        public ushort Always02;
        public ushort SectorsPerTrack;
        public ushort NumberOfHeads;
        public uint HiddenSectors;
        public uint NotUsed2;
        public uint NotUsed3;
        public ulong TotalSectors;
        public ulong MftStartLCN;
        public ulong Mft2StartLCN;
        public uint ClustersPerFileRecord;
        public uint ClustersPerIndexBlock;
        public ulong VolumeSerialNumber;
        public uint Checksum;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(
        SafeFileHandle hFile,
        [Out] byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    static bool ReadBootSector(SafeFileHandle hVolume, out NTFS_BOOT_SECTOR bootSector)
    {
        byte[] sectorData = new byte[512];
        uint bytesRead;
        bootSector = new NTFS_BOOT_SECTOR();

        if (!ReadFile(hVolume, sectorData, 512, out bytesRead, IntPtr.Zero) || bytesRead != 512)
        {
            Console.WriteLine($"Failed to read boot sector. Error: {Marshal.GetLastWin32Error()}");
            return false;
        }

        GCHandle handle = GCHandle.Alloc(sectorData, GCHandleType.Pinned);
        try
        {
            bootSector = (NTFS_BOOT_SECTOR)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(NTFS_BOOT_SECTOR));
        }
        finally
        {
            handle.Free();
        }

        return true;
    }

    static bool VerifyMFTSignature(SafeFileHandle hVolume, long mftOffset)
    {
        byte[] signature = new byte[4];
        uint bytesRead;
        long newFilePointer;

        if (!SetFilePointerEx(hVolume, mftOffset, out newFilePointer, 0))
        {
            Console.WriteLine($"Failed to set file pointer. Error: {Marshal.GetLastWin32Error()}");
            return false;
        }

        if (!ReadFile(hVolume, signature, 4, out bytesRead, IntPtr.Zero) || bytesRead != 4)
        {
            Console.WriteLine($"Failed to read MFT signature. Error: {Marshal.GetLastWin32Error()}");
            return false;
        }

        return BitConverter.ToUInt32(signature, 0) == 0x454C4946; // 'FILE' in little-endian
    }

    static void Main()
    {
        using (SafeFileHandle hVolume = CreateFile(
            @"\\.\C:",
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero))
        {
            if (hVolume.IsInvalid)
            {
                Console.WriteLine($"Failed to open volume. Error: {Marshal.GetLastWin32Error()}");
                return;
            }

            NTFS_BOOT_SECTOR bootSector;
            if (!ReadBootSector(hVolume, out bootSector))
            {
                return;
            }

            long mftOffset = (long)(bootSector.MftStartLCN * bootSector.SectorsPerCluster * bootSector.BytesPerSector);

            Console.WriteLine($"MFT Start LCN: {bootSector.MftStartLCN}");
            Console.WriteLine($"Bytes Per Sector: {bootSector.BytesPerSector}");
            Console.WriteLine($"Sectors Per Cluster: {bootSector.SectorsPerCluster}");
            Console.WriteLine($"Calculated MFT Offset: 0x{mftOffset:X}");

            if (VerifyMFTSignature(hVolume, mftOffset))
            {
                Console.WriteLine("MFT signature verified successfully.");
            }
            else
            {
                Console.WriteLine("Failed to verify MFT signature.");
            }
        }
    }
}