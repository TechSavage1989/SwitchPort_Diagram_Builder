using System.Text;
namespace HUBTechGenerator;

public static class ConfigInput
{
    public static string Read(string path)=>Decode(File.ReadAllBytes(path));
    public static string Decode(byte[] bytes)
    {
        if(bytes.Length>20_000_000)throw new InvalidOperationException("Configuration exceeds the 20 MB limit.");
        if(bytes.Length>=4)
        {
            uint magic=BitConverter.ToUInt32(bytes,0);
            if(magic==0xb1a1ac88)return MikroTikBackup.Decode(bytes);
            if(magic is 0x7291a8ef or 0x7391a8ef)throw new InvalidOperationException("This MikroTik backup is encrypted. Load an unencrypted backup or a RouterOS .rsc export.");
        }
        using var reader=new StreamReader(new MemoryStream(bytes),Encoding.UTF8,true);
        string text=reader.ReadToEnd();
        if(text.Contains('\0'))throw new InvalidOperationException("Unsupported binary configuration. Use a RouterOS .rsc export or an unencrypted RouterOS backup.");
        return text;
    }
}
