using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
        public long Initials { get; set; }
        public long FullFormat { get; set; }

        public override string ToString()
        {
            return $"FirstLast1: 0x{Convert.ToString(FirstLast1, 16)}\n" +
                $"First: 0x{Convert.ToString(First, 16)}\n" +
                $"Last: 0x{Convert.ToString(Last, 16)}\n" +
                $"FirstLast2: 0x{Convert.ToString(FirstLast2, 16)}\n" +
                $"Initials: 0x{Convert.ToString(Initials, 16)}\n" +
                $"FullFormat: 0x{Convert.ToString(FullFormat, 16)}\n";
        }
    }

    public struct Target
    {
        public string Driver { get; set; }
        public string Initials { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
    }

    public class Foni
    {

        const long MAIN_START = 0x2E5600000;
        const long OFMT_START = 0x2E5800000 ;
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

        Dictionary<string, string> intlOverride = new()
        {
            ["Kimi Räikkönen"] = "RAI"
        };

        private Dictionary<string, Offsets> offsets = new();
        private readonly ManagedProcess proc;

        public Foni(string procName)
        {
            proc = new ManagedProcess(procName);
            if (!proc.Valid)
            {
                Console.WriteLine("Could not find process");
                return;
            }
        }

        public void Run(string[] args)
        {
            if (!LoadOffsets("offsets.json"))
            {
                InitOffsets();
                WriteOffsets("offsets.json");
            }

            if (args.Length >= 1)
                ChangeNames(args[0]);
            else
                ChangeNames("default.nms");
        }

        private void InitOffsets()
        {
            offsets.Clear();
            for (int i = 0; i < drivers.Length; i++)
            {
                string driver = drivers[i];

                Offsets ofs = GetOffsetsFor(driver);
                offsets.Add(driver, ofs);

                Console.WriteLine($"Scanning for {driver,-20} ({i + 1}/{drivers.Length})");
#if DEBUG
                Console.WriteLine(ofs);
                Console.WriteLine();
#endif
            }
        }

        private Offsets GetOffsetsFor(string name)
        {
            Offsets offsets = new();

            string[] tokens = name.Split();
            string intlString = intlOverride.GetValueOrDefault(name, tokens[1][0..3].ToUpper());

            byte[] first = Encoding.UTF8.GetBytes(tokens[0]);
            byte[] last = Encoding.UTF8.GetBytes(tokens[1].ToUpper()); ;
            byte[] fullTV = Encoding.UTF8.GetBytes(tokens[0] + ' ' + tokens[1].ToUpper());
            byte[] initials = Encoding.UTF8.GetBytes(intlString);
            byte[] oFmt = Encoding.UTF8.GetBytes("{o:mixed}" + tokens[0] + "{/o} {o:upper}" + tokens[1].ToUpper() + "{/o}");

            // Find first offset
            offsets.FirstLast1 = FindString(MAIN_START, fullTV);
            offsets.First = FindString(offsets.FirstLast1 + fullTV.Length, first);
            offsets.Last = FindString(offsets.First + first.Length, last);
            offsets.FirstLast2 = FindString(offsets.Last + last.Length, fullTV);
            offsets.Initials = FindString(offsets.FirstLast2 + fullTV.Length, initials);
            offsets.FullFormat = FindString(OFMT_START, oFmt);

            return offsets;
        }

        private long FindString(long startAddr, byte[] utfArray)
        {
            uint strlen = (uint)utfArray.Length;
            // Align start address, then add 1 byte
            // (This is how these strings are aligned for some reason)
            startAddr += (8 - (startAddr % 8)) + 1;

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

        private bool WriteOffsets(string filename)
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

        private bool LoadOffsets(string filename)
        {
            if (!File.Exists(filename))
                return false;

            string jsonTextBuffer = File.ReadAllText(filename);
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

            if (proc.InfoDict["id"] == savedInfo["id"] &&
                proc.InfoDict["timeofstart"] == savedInfo["timeofstart"])
            {
                Console.WriteLine("Found previous offsets");
                return true;
            }

            return false;
        }

        private void OverwriteString(long addr, string value)
        {
            int len = value.Length;
            value = "\0\0\0" + value + "\0";

            byte[] data = Encoding.UTF8.GetBytes(value);
            data[0] = (byte)len;
            proc.Write(addr - 3, data);
        }

        private void ChangeNames(string filename)
        {
            Console.WriteLine("Applying names file: " + filename);
            string[] lines = File.ReadAllLines(filename);

            foreach (string origLine in lines)
            {
                string line = origLine.Trim();
                if (line.StartsWith('#'))
                    continue;
                if (line.Count(x => x == ':') != 1 || line.Count(x => x == ',') != 2)
                    continue;

                string[] t1 = line.Split(':');
                string[] t2 = t1[1].Split(',');

                Target tgt = new()
                {
                    Driver = t1[0].Trim(),
                    Initials = t2[0].Trim(),
                    FirstName = t2[1].Trim(),
                    LastName = t2[2].Trim()
                };

                if (!offsets.ContainsKey(tgt.Driver))
                    continue;
                if (string.IsNullOrEmpty(tgt.LastName))
                    continue;

                Offsets ofs = offsets[tgt.Driver];

                if (!string.IsNullOrEmpty(tgt.Initials) && tgt.Initials.Length == 3)
                    OverwriteString(ofs.Initials, tgt.Initials);
                OverwriteString(ofs.First, tgt.FirstName);
                OverwriteString(ofs.Last, tgt.LastName);
                OverwriteString(ofs.FirstLast1, tgt.FullName);
                OverwriteString(ofs.FirstLast2, tgt.FullName);
            }
        }
    }
}
