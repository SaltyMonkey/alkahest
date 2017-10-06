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
                { Region.DE, 321154 },
                { Region.FR, 321154 },
                { Region.JP, 321161 },
                { Region.KR, 321110 },
                { Region.NA, 321152 },
                { Region.RU, 320861 },
                { Region.TW, 321159 },
                { Region.UK, 321154 }
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

                        ushort code = 0;
                        if (parts.Length == 3)
                        {
                            code = ushort.Parse(parts[2]);
                        }
                        else
                        {
                            code = code = ushort.Parse(parts[1]);

                        }
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
