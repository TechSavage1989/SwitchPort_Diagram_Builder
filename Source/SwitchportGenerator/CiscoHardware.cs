using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public static class CiscoHardware
{
    public sealed record Socket(int Number,int Column,int Row,string Media,string Interface,string Label,string[] Aliases);
    public sealed record Model(string Code,string Name,string Source,IReadOnlyList<Socket> Sockets);
    const string Guide3650="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3650/hardware/installation/guide/Cat3650hig_book/HIGOVERV.html";
    const string Guide9500="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9500/hardware/install/b_catalyst_9500_hig/9500_product-overview.html";
    public static IReadOnlyList<Model> Models {get;}=new[]{Build3650(),Build9500(24),Build9500(48)};
    public static string CanonicalModel(string value)=>Regex.Replace(value.Trim().ToUpperInvariant(),@"-(?:L|S|E|A)$","");
    public static Model? Find(string code)=>Models.FirstOrDefault(m=>m.Code==CanonicalModel(code));
    public static bool IsModel(string value)=>Regex.IsMatch(value,@"^(?:WS-C\d[\w-]*|C9\d{3}-[\w-]+)$",RegexOptions.IgnoreCase);
    public static string Interface(Socket socket,string member)=>socket.Interface.Replace("{m}",member);
    public static IEnumerable<string> Names(Socket socket,string member)=>new[]{Interface(socket,member)}.Concat(socket.Aliases.Select(a=>a.Replace("{m}",member)));
    static Model Build3650()
    {
        var ports=Enumerable.Range(1,48).Select(n=>new Socket(n,(n-1)/2,(n-1)%2,"RJ-45","GigabitEthernet{m}/0/"+n,n.ToString(),Array.Empty<string>())).ToList();
        for(int n=1;n<=4;n++)ports.Add(new(48+n,24+n-1,0,n<=2?"SFP":"SFP+",(n<=2?"GigabitEthernet":"TenGigabitEthernet")+"{m}/1/"+n,"U"+n,n<=2?Array.Empty<string>():new[]{"GigabitEthernet{m}/1/"+n}));
        return new("WS-C3650-48PD","Catalyst 3650 48-port PoE+ with 2 SFP and 2 SFP+ uplinks",Guide3650,ports);
    }
    static Model Build9500(int count)
    {
        var ports=Enumerable.Range(1,count).Select(n=>new Socket(n,(n-1)/2,(n-1)%2,"SFP28","TwentyFiveGigE{m}/0/"+n,n.ToString(),Array.Empty<string>())).ToList();
        for(int n=1;n<=4;n++)ports.Add(new(count+n,count/2+(n-1)/2,(n-1)%2,"QSFP28","HundredGigE{m}/0/"+(count+n),(count+n).ToString(),Array.Empty<string>()));
        return new("C9500-"+count+"Y4C","Catalyst 9500 "+count+" SFP28 and 4 QSFP28 ports",Guide9500,ports);
    }
}
