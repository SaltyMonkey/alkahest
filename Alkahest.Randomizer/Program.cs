using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Alkahest.Randomizer
{
    class Program
    {
        private static readonly string AlkahestName = "\\alkahest-server.exe";
        private static readonly byte[] RandomizeString =
        {
            0x53, 0x00, 0x75, 0x00, 0x70, 0x00, 0x65, 0x00, 0x72, 0x00, 0x55, 0x00, 0x6E, 0x00, 0x69, 0x00, 0x71, 0x00, 0x75, 0x00, 0x65, 0x00, 0x53, 0x00, 0x74,
            0x00, 0x72, 0x00, 0x69, 0x00, 0x6E, 0x00, 0x67, 0x00, 0x45, 0x00, 0x61, 0x00, 0x73, 0x00, 0x69, 0x00, 0x6C, 0x00, 0x79, 0x00, 0x44, 0x00, 0x65, 0x00,
            0x74, 0x00, 0x65, 0x00, 0x63, 0x00, 0x74, 0x00, 0x61, 0x00, 0x62, 0x00, 0x6C, 0x00, 0x65, 0x00, 0x54, 0x00, 0x6F, 0x00, 0x42, 0x00, 0x65, 0x00, 0x41,
            0x00, 0x62, 0x00, 0x6C, 0x00, 0x65, 0x00, 0x54, 0x00, 0x6F, 0x00, 0x52, 0x00, 0x61, 0x00, 0x6E, 0x00, 0x64, 0x00, 0x6F, 0x00, 0x6D, 0x00, 0x69, 0x00,
            0x7A, 0x00, 0x65, 0x00, 0x54, 0x00, 0x68, 0x00, 0x65, 0x00, 0x50, 0x00, 0x72, 0x00, 0x6F, 0x00, 0x67, 0x00, 0x72, 0x00, 0x61, 0x00, 0x6D, 0x00, 0x41,
            0x00, 0x6E, 0x00, 0x64, 0x00, 0x42, 0x00, 0x79, 0x00, 0x70, 0x00, 0x61, 0x00, 0x73, 0x00, 0x73, 0x00, 0x53, 0x00, 0x69, 0x00, 0x67, 0x00, 0x6E, 0x00,
            0x61, 0x00, 0x74, 0x00, 0x75, 0x00, 0x72, 0x00, 0x65, 0x00, 0x42, 0x00, 0x61, 0x00, 0x73, 0x00, 0x65, 0x00, 0x64, 0x00, 0x42, 0x00, 0x6C, 0x00, 0x6F,
            0x00, 0x63, 0x00, 0x6B, 0x00
        };

        private static readonly byte[] DosStubPart =
        {
            0x54, 0x68, 0x69, 0x73, 0x20, 0x70, 0x72, 0x6f, 0x67, 0x72, 0x61, 0x6d, 0x20, 0x63, 0x61, 0x6e,
            0x6e, 0x6f, 0x74, 0x20, 0x62, 0x65, 0x20, 0x72, 0x75, 0x6e, 0x20, 0x69, 0x6e, 0x20, 0x44, 0x4f,
            0x53, 0x20, 0x6d, 0x6f, 0x64, 0x65
        };

        static void Main(string[] args)
        {
            CheckReplace();
        }

        private static char RandomChar()
        {
            var chars = "&^ZYXWVUTSRQPONMLKJIHGFEDCBA:;?0987654321zyxwvutsrqponmlkjihgfedcba*!@#%$".ToCharArray();
            char charForReturn = ' ';
            using (var r = new RNGCryptoServiceProvider())
            {
                while (charForReturn == ' ')
                {
                    byte[] b = new byte[1];
                    r.GetBytes(b);
                    char generatedCharacter = (char) b[0];
                    if (chars.Contains(generatedCharacter))
                    {
                        charForReturn = generatedCharacter;
                    }
                }
            }
            return charForReturn;

        }

        private static long RandomLong(long min, long max)
        {
            var rand = new RNGCryptoServiceProvider();
            var buf = new byte[8];
            rand.GetBytes(buf);
            var longRand = BitConverter.ToInt64(buf, 0);

            return Math.Abs(longRand % (max - min)) + min;
        }

        public static KeyValuePair<long, long>? DetectStringInBinary(FileStream stream)
        {
            var byteCheckPosition = 0;
            long beginPosition = 0;
            while (stream.Position < stream.Length)
            {
                if (byteCheckPosition == 0) { beginPosition = stream.Position; }

                if (stream.ReadByte() == RandomizeString[byteCheckPosition]) { byteCheckPosition++; }
                else { byteCheckPosition = 0; }
                if (byteCheckPosition == RandomizeString.Length) { return new KeyValuePair<long, long>(beginPosition, stream.Position - 1); }
            }
            return null;
        }

        public static bool DetectDosStub(FileStream stream)
        {
            int startPosition = 78;
            int posInDosStubPart = 0;
            for (stream.Position = startPosition; stream.Position < startPosition + DosStubPart.Length - 1; posInDosStubPart++ )
            {
                
                if (DosStubPart[posInDosStubPart] != (byte)stream.ReadByte())
                    return false;


            }
            stream.Position = 0;
            return true;
        }

        public static void RandomizeDosStub(FileStream stream)
        {
            int startPosition = 78 ;
            for (stream.Position = startPosition; stream.Position < startPosition + DosStubPart.Length - 1; )
            {
                stream.WriteByte((byte)RandomChar());

            }
            stream.Position = 0;
        }

        public static void Randomize(FileStream stream, KeyValuePair<long, long> positions)
        {
            var beginRandomize = RandomLong(positions.Key, positions.Value - 2);
            var sizeRandomize = RandomLong(2, positions.Key - beginRandomize);

            stream.Position = beginRandomize;
            if (stream.ReadByte() != 0) { stream.Position--; }
            while (stream.Position < beginRandomize + sizeRandomize)
            {
                stream.WriteByte(Convert.ToByte(RandomChar()));
                stream.Position = stream.Position + 2;
            }
        }

        public static void CheckReplace()
        {
            using (var stream = new FileStream($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}{AlkahestName}", FileMode.Open,
                FileAccess.ReadWrite, FileShare.None))
            {
                var dosStubStringDetected = DetectDosStub(stream);
                if (dosStubStringDetected)
                {
                    RandomizeDosStub(stream);
                }
                var stringDetected = DetectStringInBinary(stream);
                if (stringDetected == null)
                {
                    Console.WriteLine("The file as already been randomized or randomization impossible. Exiting");
                    Console.ReadKey();
                    return;
                }
                Randomize(stream, stringDetected.Value);
                Console.WriteLine("The file as been successfully randomized. Exiting");
                Console.ReadKey();
            }
        }

    }
}
