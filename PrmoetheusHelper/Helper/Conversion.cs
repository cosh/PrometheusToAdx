using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PrmoetheusHelper.Helper
{
    public class Conversion
    {
        public static byte[] DecompressBody(Stream body)
        {
            MemoryStream ms = new MemoryStream();
            body.CopyTo(ms);

            var decompressor = new Snappy.Sharp.SnappyDecompressor();
            var source = ms.ToArray();

            var decompressed = decompressor.Decompress(source, 0, source.Length);

            return decompressed;
        }
    }
}
