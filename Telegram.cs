public class Telegram
{
    private byte[] _header;
    private byte[] _payload;
    private byte[] _bytes;

    public Telegram(byte[] header, byte[] payload)
    {
        _header = header;
        _payload = payload.Reverse().ToArray();
        _bytes = _header.Concat(_payload).ToArray();
    }

    private string DecimalToHex(byte d)
    {
        return d.ToString("x2");
    }

    public string GetBytes
    {
        get
        {
            return string.Concat(_bytes.Select(b => DecimalToHex(b)));
        }
    }
}