using System.Diagnostics;

namespace ShopPi;

public static class Crc8
{
    // x8 + x7 + x6 + x4 + x2 + 1
    public const byte Poly = 0xEB;

    public static byte ComputeChecksum(IReadOnlyCollection<byte> bytes, byte poly = Poly)
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new NotSupportedException("Arduino is little endian.");
        }
        
        byte crc = 0;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 8; i > 0; i--)
            {
                if ((crc & (1 << 7)) > 0)
                {
                    crc <<= 1;
                    crc ^= poly;
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        return crc;
    }
}