using Snappy.Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PrmoetheusHelper.Helper
{
    public class Conversion
    {
        private static SnappyDecompressor decompressor = new SnappyDecompressor();

        public static byte[] DecompressBody(Stream body)
        {
            MemoryStream ms = new MemoryStream();
            body.CopyTo(ms);

            var source = ms.ToArray();

            if (source != null && source.Length > 0)
            {
                var decompressed = decompressor.Decompress(source, 0, source.Length);

                return decompressed;
            }

            return null;
        }
    }
}
