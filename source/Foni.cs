using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace foni
{
    public struct Offsets
    {
        public long FirstLast1 { get; set; }
        public long First { get; set; }
        public long Last { get; set; }
        public long FirstLast2 { get; set; }
        public long FullFormat { get; set; }

        public override string ToString()
        {
            return $"FirstLast1: 0x{Convert.ToString(FirstLast1, 16)}\n" +
                $"First: 0x{Convert.ToString(First, 16)}\n" +
                $"Last: 0x{Convert.ToString(Last, 16)}\n" +
                $"FirstLast2: 0x{Convert.ToString(FirstLast2, 16)}\n" +
                $"FullFormat: 0x{Convert.ToString(FullFormat, 16)}\n";
        }
    }

    public class Foni
    {

        const long MAIN_START = 0x2E5600000 + 1;
        const long OFMT_START = 0x2E5800000 + 1;
        const long SEARCH_LIMIT = 0x400000000;

        readonly string[] drivers =
        {
            "Carlos Sainz",
            "Daniel Ricciardo",
            "Fernando Alonso",
            "Kimi Räikkönen",
            "Lewis Hamilton",
            "Max Verstappen",
            "Sebastian Vettel",
            "Sergio Perez",
            "Valtteri Bottas",
            "Esteban Ocon",
            "Lance Stroll",
            "George Russell",
            "Lando Norris",
            "Charles Leclerc",
            "Pierre Gasly",
            "Nicholas Latifi",
            "Antonio Giovinazzi",
            "Nikita Mazepin",
            "Mick Schumacher",
            "Yuki Tsunoda"
        };

        private Dictionary<string, Offsets> offsets = new();

        public void Run()
        {
            using ManagedProcess proc = new("F1_2021_dx12");
            if (!proc.Valid)
            {
                Console.WriteLine("Could not find process");
                return;
            }

            if (!LoadOffsets("offsets.json", proc))
            {
                InitOffsets(proc);
                WriteOffsets("offsets.json", proc);
            }
        }

        private void InitOffsets(ManagedProcess proc)
        {
            offsets.Clear();
            for (int i = 0; i < drivers.Length; i++)
            {
                string driver = drivers[i];

                Offsets ofs = GetOffsetsFor(proc, driver);
                offsets.Add(driver, ofs);

                Console.WriteLine($"Scanning for {driver,-20} ({i + 1}/{drivers.Length})");
#if DEBUG
                Console.WriteLine(ofs);
                Console.WriteLine();
#endif
            }
        }

        private Offsets GetOffsetsFor(ManagedProcess proc, string name)
        {
            Offsets offsets = new();

            string[] tokens = name.Split();
            byte[] first = Encoding.UTF8.GetBytes(tokens[0]);
            byte[] last = Encoding.UTF8.GetBytes(tokens[1].ToUpper()); ;
            byte[] fullTV = Encoding.UTF8.GetBytes(tokens[0] + ' ' + tokens[1].ToUpper());
            byte[] oFmt = Encoding.UTF8.GetBytes("{o:mixed}" + tokens[0] + "{/o} {o:upper}" + tokens[1].ToUpper() + "{/o}");

            // Find first offset
            offsets.FirstLast1 = FindString(proc, MAIN_START, fullTV);
            offsets.First = FindString(proc, offsets.FirstLast1 + 8, first);
            offsets.Last = FindString(proc, offsets.First + 8, last);
            offsets.FirstLast2 = FindString(proc, offsets.Last + 8, fullTV);
            offsets.FullFormat = FindString(proc, OFMT_START, oFmt);

            return offsets;
        }

        private long FindString(ManagedProcess proc, long startAddr, byte[] utfArray)
        {
            uint strlen = (uint)utfArray.Length;

            for (long addr = startAddr; addr < startAddr + SEARCH_LIMIT; addr += 8)
            {
                //Console.WriteLine(Convert.ToString(addr, 16));
                if (
                    proc.Read(addr, 1)?[0] == utfArray[0] && // Check initial character
                    proc.Read(addr+1, 1)?[0] == utfArray[1] && // Check second character
                    proc.Read(addr, strlen).SequenceEqual(utfArray) && // Check full string
                    proc.Read(addr - 3, 3).SequenceEqual(new byte[] { (byte)strlen, 0, 0 }) // Check that length prefix is present
                    )
                {
                    return addr;
                }
            }

            return 0;
        }

        private bool WriteOffsets(string filename, ManagedProcess proc)
        {
            var objs = new Dictionary<string, object>
            {
                ["process"] = proc.InfoDict,
                ["drivers"] = offsets
            };

            var opts = new JsonSerializerOptions();
            opts.WriteIndented = true;
            string result = JsonSerializer.Serialize(objs, opts);
            File.WriteAllText(filename, result, Encoding.UTF8);
            return true;
        }

        private bool LoadOffsets(string filename, ManagedProcess proc)
        {
            if (!File.Exists(filename))
                return false;

            string jsonTextBuffer = File.ReadAllText(filename);
            var opts = new JsonSerializerOptions();
            var root = JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(jsonTextBuffer)!;

            Dictionary<string, long> savedInfo = new();

            foreach (var pair in root)
            {
                switch(pair.Key)
                {
                    case "process":
                        savedInfo = JsonSerializer.Deserialize<Dictionary<string, long>>(pair.Value)!;
                        break;

                    case "drivers":
                        offsets = JsonSerializer.Deserialize<Dictionary<string, Offsets>>(pair.Value)!;
                        break;
                }
            }

            if (proc.InfoDict["id"] == savedInfo["id"] && proc.InfoDict["timeofstart"] == savedInfo["timeofstart"])
            {
                Console.WriteLine("Found previous offsets");
                return true;
            }

            return false;
        }
    }
}
