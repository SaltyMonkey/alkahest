using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Alkahest.Core.Net.Protocol.OpCodes
{
    public abstract class OpCodeTable
    {
        public uint Version { get; }

        public IReadOnlyDictionary<ushort, string> OpCodeToName { get; }

        public IReadOnlyDictionary<string, ushort> NameToOpCode { get; }

        public static IReadOnlyDictionary<Region, uint> Versions { get; } =
            new Dictionary<Region, uint>
            {
                { Region.DE, 320858 },
                { Region.FR, 320858 },
                { Region.JP, 313623 },
                { Region.KR, 313523 },
                { Region.NA, 320857 },
                { Region.RU, 320861 },
                { Region.TW, 313623 },
                { Region.UK, 320858 }
            };

        internal OpCodeTable(bool opCodes, uint version)
        {
            if (!Versions.Values.Contains(version))
                throw new ArgumentOutOfRangeException(nameof(version));

            Version = version;

            //var asm = Assembly.GetExecutingAssembly();
            var path = $@"{AppDomain.CurrentDomain.BaseDirectory}\{nameof(Net)}\{nameof(Protocol)}\{nameof(OpCodes)}\{(opCodes ? "opc" : "smt")}_{Version}.txt";
            var codeToName = new Dictionary<ushort, string>();
            var nameToCode = new Dictionary<string, ushort>();

            using (var stream = File.Open(path, FileMode.Open))
            {
                using (var reader = new StreamReader(stream))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        //Support of comments
                        if (line.Length <=3) continue;
                        if (line.Contains("#"))
                        {
                            var symbolIndex = line.IndexOf("#", StringComparison.Ordinal);
                            if (symbolIndex == 0) continue;
                            else
                            {
                               line = line.Substring(0, symbolIndex - 1);
                            }
                        }
                        //-------------------
                        var parts = line.Split(' ');
                              // This is just a marker value.
                        if (!opCodes && parts[0] == "SMT_MAX")
                                continue;

                        var code = ushort.Parse(parts[2]);

                        codeToName.Add(code, parts[0]);
                        nameToCode.Add(parts[0], code);
                     
                    }
                }
            }

            NameToOpCode = nameToCode;
            OpCodeToName = codeToName;
        }
    }
}
