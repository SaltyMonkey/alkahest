using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Alkahest.Core.Cryptography;
using Alkahest.Core.IO;
using Alkahest.Core.Logging;

namespace Alkahest.Extractor.Commands
{
    sealed class DecryptDownloadedResourcesCommand : ICommand
    {

        static readonly Log _log = new Log(typeof(DecryptDownloadedResourcesCommand));

        public string Name => "decrypt-res";

        public string Syntax =>
            $"{Name} <downloaded resources file>";

        public string Description =>
            "Decrypt downloaded resources dat file.";

        public int RequiredArguments => 1;

        public void Run(string output, string[] args)
        {
            //thx Caali
            var key = new byte[32]
            {
                0x1C, 0x24, 0x00, 0x00, 0x1F, 0x04, 0x00, 0x00, 0x72, 0xF4, 0x00, 0x00, 0x1D, 0x62, 0x00, 0x00, 0xBD,
                0xA8, 0x00, 0x00, 0xDB, 0xA7, 0x00, 0x00, 0x01, 0x30, 0x00, 0x00, 0x33, 0x27, 0x00, 0x00
            };
            //thx Gl0 (https://github.com/Gl0)
            byte[] magicPattern = {0x95, 0x74, 0x4E, 0x47, 0x12, 0x0E};

            var input = args[0];
            //TODO: Add normal path convertion
            if (output == null)
                output = Path.ChangeExtension(input, "dec");

            _log.Basic("Decrypting {0}...", input);
         
             var resource = File.ReadAllBytes(input);
            var searcher = new BoyerMoore(magicPattern).SearchAll(resource);
            var j = 0;
            var laststart = 0;
            for (var i = 0; i < resource.Length; ++i)
            {
                resource[i] ^= key[j % 32];
                j++;
                if (searcher.Contains(i))
                {
                    var block = new byte[i - laststart];
                    Array.Copy(resource, laststart, block, 0, i - laststart);
                    if (laststart != 0) block[0] = 0x89;
                    File.WriteAllBytes(Path.GetDirectoryName(output) + "." + laststart + (laststart == 0 ? ".txt" : ".png"), block);
                    j = 1;
                    laststart = i;
                }
            }

            _log.Basic("Decrypted downloaded resources to {0}", Path.GetDirectoryName(output));
        }

        public class BoyerMoore
        {
            private readonly int[] _jumpTable;
            private readonly byte[] _pattern;
            private readonly int _patternLength;
            public BoyerMoore(byte[] pattern)
            {
                _pattern = pattern;
                _jumpTable = new int[256];
                _patternLength = _pattern.Length;
                for (var index = 0; index < 256; index++)
                    _jumpTable[index] = _patternLength;
                for (var index = 0; index < _patternLength - 1; index++)
                    _jumpTable[_pattern[index]] = _patternLength - index - 1;
            }
            public unsafe int Search(byte[] byteArray, int startIndex = 0)
            {
                var index = startIndex;
                var limit = byteArray.Length - _patternLength;
                var patternLengthMinusOne = _patternLength - 1;
                fixed (byte* pointerToByteArray = byteArray)
                {
                    var pointerToByteArrayStartingIndex = pointerToByteArray + startIndex;
                    fixed (byte* pointerToPattern = _pattern)
                    {
                        while (index <= limit)
                        {
                            var j = patternLengthMinusOne; while (j >= 0 && pointerToPattern[j] == pointerToByteArrayStartingIndex[index + j])
                                j--;
                            if (j < 0)
                                return index;
                            index += Math.Max(_jumpTable[pointerToByteArrayStartingIndex[index + j]] - _patternLength + 1 + j, 1);
                        }
                    }
                }
                return -1;
            }
            public unsafe List<int> SearchAll(byte[] byteArray, int startIndex = 0)
            {
                var index = startIndex;
                var limit = byteArray.Length - _patternLength;
                var patternLengthMinusOne = _patternLength - 1;
                List<int> list = new List<int>();
                fixed (byte* pointerToByteArray = byteArray)
                {
                    var pointerToByteArrayStartingIndex = pointerToByteArray + startIndex;
                    fixed (byte* pointerToPattern = _pattern)
                    {
                        while (index <= limit)
                        {
                            var j = patternLengthMinusOne; while (j >= 0 && pointerToPattern[j] == pointerToByteArrayStartingIndex[index + j])
                                j--;
                            if (j < 0)
                                list.Add(index);
                            index += Math.Max(_jumpTable[pointerToByteArrayStartingIndex[index + j]] - _patternLength + 1 + j, 1);
                        }
                    }
                }
                return list;
            }
            public int SuperSearch(byte[] byteArray, int nth, int start = 0)
            {
                var e = start;
                var c = 0;
                do
                {
                    e = Search(byteArray, e);
                    if (e == -1)
                        return -1;
                    c++;
                    e++;
                } while (c < nth);
                return e - 1;
            }
        }
    }
}
