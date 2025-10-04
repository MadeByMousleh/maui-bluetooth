public class FlashRow
{
    public byte ArrayID { get; }
    public ushort RowNumber { get; }
    public ushort DataLength { get; }
    public byte[] Data { get; }
    public byte Checksum { get; }

    // [1-byte ArrayID][2-byte RowNumber][2-byte DataLength][N-byte Data][1byte Checksum]
    public FlashRow(byte arrayID, ushort rowNumber, ushort dataLength, byte[] data, byte checksum)
    {
        ArrayID = arrayID;
        RowNumber = rowNumber;
        DataLength = dataLength;
        Data = data;
        Checksum = checksum;
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[1 + 2 + 2 + Data.Length + 1];
        bytes[0] = ArrayID;
        bytes[1] = (byte)(RowNumber >> 8);
        bytes[2] = (byte)(RowNumber & 0xFF);
        bytes[3] = (byte)(DataLength >> 8);
        bytes[4] = (byte)(DataLength & 0xFF);
        Buffer.BlockCopy(Data, 0, bytes, 5, Data.Length);
        bytes[bytes.Length - 1] = Checksum;
        return bytes;
    }
}