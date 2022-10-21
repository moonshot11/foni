using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace foni
{
    public class ManagedProcess : IDisposable
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern int CloseHandle(IntPtr hProcess);

        private const uint ALL_ACCESS = 0x001F0FFF;

        private readonly IntPtr hProc = IntPtr.Zero;
        private readonly Process proc;
        public bool Valid { get; private set; }

        public ManagedProcess(string procName)
        {
            Process[] foundProcs = Process.GetProcessesByName(procName);
            if (foundProcs.Length < 1)
                return;
            proc = foundProcs[0];
#if DEBUG
            Console.WriteLine("Process ID: " + proc.Id);
            Console.WriteLine("Process ST: " + proc.StartTime.Ticks);
            Console.WriteLine();
#endif
            hProc = OpenProcess(ALL_ACCESS, false, proc.Id);
            
            if (hProc != IntPtr.Zero)
                Valid = true;
        }

        ~ManagedProcess()
        {
            Dispose();
        }

        public void Write(long writeAddr, byte[] data)
        {
            if (!Valid)
                throw new Exception("ManagedProcess not valid - could not write");
            WriteProcessMemory(hProc, new IntPtr(writeAddr), data, (uint)data.Length, out int _);
        }

        public byte[] Read(long readAddr, uint nSize)
        {
            if (!Valid)
                throw new Exception("ManagedProcess not valid - could not write");
            byte[] result = new byte[nSize];
            ReadProcessMemory(hProc, new IntPtr(readAddr), result, nSize, out _);
            return result;
        }
        
        public void Dispose()
        {
            if (Valid)
                return;
            CloseHandle(hProc);
            Valid = false;
        }

        public Dictionary<string, long> InfoDict
        {
            get
            {
                return new Dictionary<string, long>
                {
                    ["id"] = proc.Id,
                    ["timeofstart"] = proc.StartTime.Ticks
                };
            }
            
        }
    }
}
