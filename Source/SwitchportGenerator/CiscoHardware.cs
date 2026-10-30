using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public static class CiscoHardware
{
    public sealed record Socket(int Number,double Column,int Row,string Media,string Interface,string Label,string[] Aliases,bool ConfiguredInterface=false);
    public sealed record Model(string Code,string Name,string Source,IReadOnlyList<Socket> Sockets);
    const string Guide3650="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3650/hardware/installation/guide/Cat3650hig_book/HIGOVERV.html";
    const string Guide9500="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9500/hardware/install/b_catalyst_9500_hig/9500_product-overview.html";
    public const string Shared3560E48Profile="CISCO-3560E-48-SHARED";
    public const string Model3560E48="WS-C3560E-48PD-SF";
    const string Guide3560E="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/software/release/15-2_1_e/configuration/guide/scg3750x/swint.html";
    static readonly Model shared3560=Build3560E(Shared3560E48Profile,"Catalyst 3560-E 48-port shared layout; uplink module unconfirmed");
    public static IReadOnlyList<Model> Models {get;}=Build3650Models().Concat(new[]{Build9500(24),Build9500(48),Build3560E(Model3560E48,"Catalyst 3560E-48PD-SF; uplink module unconfirmed")}).Concat(CiscoModuleCatalog.BaseModels).ToArray();
    public static string CanonicalModel(string value)=>Regex.Replace(value.Trim().ToUpperInvariant(),@"-(?:L|S|E|A)$","");
    public static Model? Find(string code)=>code.Equals(Shared3560E48Profile,StringComparison.OrdinalIgnoreCase)?shared3560:Models.FirstOrDefault(m=>m.Code==CanonicalModel(code));
    public static bool Is3560E48(string code)=>code.Equals(Shared3560E48Profile,StringComparison.OrdinalIgnoreCase)||CanonicalModel(code)==Model3560E48;
    public static bool CompatibleSelection(string detected,string selected)=>detected.Equals(Shared3560E48Profile,StringComparison.OrdinalIgnoreCase)&&CanonicalModel(selected)==Model3560E48;
    public static bool Complete3560E48Signature(string text,ISet<string> dataNames,ISet<string> names)=>Regex.IsMatch(Regex.Match(text,@"(?im)^version ([^\r\n]+)$").Groups[1].Value.Trim(),@"^15\.\d+$")&&names.Contains("FastEthernet0")&&dataNames.SetEquals(shared3560.Sockets.Select(s=>Interface(s,"1")));
    public static bool IsModel(string value)=>Regex.IsMatch(value,@"^(?:WS-C\d[\w-]*|C9\d{3}(?:X|L|LM)?-[\w-]+)$",RegexOptions.IgnoreCase);
    public static string Interface(Socket socket,string member)=>socket.Interface.Replace("{m}",member);
    public static IEnumerable<string> Names(Socket socket,string member)=>new[]{Interface(socket,member)}.Concat(socket.Aliases.Select(a=>a.Replace("{m}",member)));
    // Provisioning is not an exact power-supply bundle PID. Only these audited,
    // same-port PoE bundle pairs may be refined by operator hardware evidence.
    public static bool CompatibleProvision(string detected,string selected)
    {
        string a=CanonicalModel(detected),b=CanonicalModel(selected);
        return new[]{new[]{"WS-C3650-48PD","WS-C3650-48FD"},new[]{"WS-C3650-48PS","WS-C3650-48FS"},new[]{"WS-C3650-48PQ","WS-C3650-48FQ"}}
            .Any(pair=>pair.Contains(a)&&pair.Contains(b));
    }
    static IEnumerable<Model> Build3650Models()
    {
        // Exact model-table entries, not a Cartesian product of suffixes.
        foreach(string suffix in new[]{"24TS","48TS","24PS","48PS","48FS","24TD","48TD","24PD","48PD","48FD","48FQ","48PQ","48TQ","24PDM","48FQM"})
        {
            int count=int.Parse(suffix[..2]);string uplinks=suffix.TrimEnd('M')[^1].ToString();
            yield return Build3650(suffix,count,uplinks);
        }
    }
    static Model Build3650(string suffix,int count,string uplinks)
    {
        var ports=Enumerable.Range(1,count).Select(n=>new Socket(n,(count==24?13:0)+(n-1)/2+.5*((n-1)/12),(n-1)%2,"RJ-45","GigabitEthernet{m}/0/"+n,n.ToString(),Array.Empty<string>())).ToList();
        for(int n=1;n<=4;n++)
        {
            bool ten=uplinks=="Q"||uplinks=="D"&&n>2;
            ports.Add(new(count+n,26.25+n-1,1,ten?"SFP+":"SFP",(ten?"TenGigabitEthernet":"GigabitEthernet")+"{m}/1/"+n,"U"+n,ten?new[]{"GigabitEthernet{m}/1/"+n}:Array.Empty<string>()));
        }
        string power=suffix[2]=='T'?"data":suffix[2]=='F'?"Full PoE+":"PoE+";
        return new("WS-C3650-"+suffix,"Catalyst 3650 "+count+"-port "+power+" with "+(uplinks=="S"?"4 SFP":uplinks=="Q"?"4 SFP+":"2 SFP and 2 SFP+")+" fixed uplinks",Guide3650,ports);
    }
    static Model Build9500(int count)
    {
        var ports=Enumerable.Range(1,count).Select(n=>new Socket(n,(n-1)/2+.5*((n-1)/24),(n-1)%2,"SFP28","TwentyFiveGigE{m}/0/"+n,n.ToString(),Array.Empty<string>())).ToList();
        for(int n=1;n<=4;n++)ports.Add(new(count+n,25.25+(n-1)/2,(n-1)%2,"QSFP28","HundredGigE{m}/0/"+(count+n),(count+n).ToString(),Array.Empty<string>()));
        return new("C9500-"+count+"Y4C","Catalyst 9500 "+count+" SFP28 and 4 QSFP28 ports",Guide9500,ports);
    }
    static Model Build3560E(string code,string name)
    {
        var ports=Enumerable.Range(1,48).Select(n=>new Socket(n,(n-1)/2+.5*((n-1)/12),(n-1)%2,"RJ-45","GigabitEthernet0/"+n,n.ToString(),Array.Empty<string>())).ToList();
        // This is an alternatives table, not installed socket geometry. Each bay
        // can expose one Te interface OR its two Gi interfaces through TwinGig.
        for(int bay=0;bay<2;bay++)
        {
            for(int index=0;index<2;index++)
            {int number=49+bay*2+index;ports.Add(new(49+bay*3+index,index,bay,"Unspecified","GigabitEthernet0/"+number,"Gi"+number,Array.Empty<string>(),true));}
            ports.Add(new(51+bay*3,2,bay,"Unspecified","TenGigabitEthernet0/"+(bay+1),"Te"+(bay+1),Array.Empty<string>(),true));
        }
        return new(code,name,Guide3560E,ports);
    }
}
