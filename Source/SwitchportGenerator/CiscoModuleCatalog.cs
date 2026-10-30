using System.Text.RegularExpressions;
namespace SwitchportGenerator;

public sealed class InstalledModule
{
    public string Device {get;set;}="";
    public string Slot {get;set;}="";
    public string Sku {get;set;}="";
    public bool OperatorSupplied {get;set;}
}

/// <summary>Installed network modules, not a list of every software interface as physical sockets.</summary>
public static class CiscoModuleCatalog
{
    const string XGuide="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/hardware/installation/guide/3750x_3560x_HIG/HIGOVERV.html";
    const string XInterfaces="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/software/release/15-2_1_e/configuration/guide/scg3750x/swint.html";
    const string EGuide="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750x_3560x/software/release/15-0_2a_se6/release/notes/OL25302.html";
    const string Guide3850="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3850/hardware/installation/guide/b_c3850_hig/m_install_netmod.html";
    const string Base3850="https://www.cisco.com/c/en/us/products/collateral/switches/catalyst-3850-series-switches/data_sheet_c78-720918.pdf";
    const string Guide9200="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9200/hardware/install/b-c9200-hig/product_overview.html";
    const string Guide9300="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9300/hardware/install/b_c9300_hig/Installing-a-network-module.html";
    const string Base9300="https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst9300/hardware/install/b_c9300_hig/Product-overview.html";
    sealed record Definition(string Sku,string Name,string Family,string Source,Func<string,string,List<CiscoHardware.Socket>> Layout);
    static CiscoHardware.Socket S(int n,string kind,string media,int column=-1,int row=0)=>new(n,column<0?n-1:column,row,media,kind+"{m}/1/"+n,Short(kind)+n,kind=="TwentyFiveGigE"?new[]{"TwentyFiveGigabitEthernet{m}/1/"+n}:Array.Empty<string>());
    static string Short(string kind)=>kind switch{"GigabitEthernet"=>"Gi","TenGigabitEthernet"=>"Te","TwentyFiveGigE"=>"Twe","FortyGigabitEthernet"=>"Fo",_=>"Hu"};
    static List<CiscoHardware.Socket> Ports(int count,string kind,string media)=>Enumerable.Range(1,count).Select(n=>S(n,kind,media,count==8?(n-1)/2:n-1,count==8?(n-1)%2:0)).ToList();
    static List<CiscoHardware.Socket> X2(string sku,string slot)
    {
        int bay=slot=="X2-1"?0:1;
        if(sku=="CVR-X2-SFP")return Enumerable.Range(0,2).Select(i=>new CiscoHardware.Socket(i+1,i,0,"SFP","GigabitEthernet0/"+(49+bay*2+i),"Gi"+(49+bay*2+i),Array.Empty<string>())).ToList();
        return new(){new(1,0,0,sku=="CVR-X2-SFP10G"?"SFP+":"X2","TenGigabitEthernet0/"+(bay+1),"Te"+(bay+1),Array.Empty<string>())};
    }
    static readonly List<Definition> definitions=BuildDefinitions();
    static List<Definition> BuildDefinitions()
    {
        var result=new List<Definition>();
        void Add(string sku,string name,string family,string source,Func<string,string,List<CiscoHardware.Socket>> layout)=>result.Add(new(sku,name,family,source,layout));
        Add("CVR-X2-SFP","TwinGig converter, 2 x 1G SFP","3560E",EGuide,(_,slot)=>X2("CVR-X2-SFP",slot));
        Add("CVR-X2-SFP10G","OneX converter, 1 x 10G SFP+","3560E","https://www.cisco.com/c/en/us/td/docs/switches/lan/catalyst3750e_3560e/software/release/12-2_53_se/release/notes/OL21139.html",(_,slot)=>X2("CVR-X2-SFP10G",slot));
        foreach(string optic in new[]{"SR","LR","ER","CX4","LX4","LRM","ZR"})
        {string sku="X2-10GB-"+optic;Add(sku,"10G X2 "+optic+" transceiver, 1 socket","3560E",EGuide,(_,slot)=>X2(sku,slot));}
        Add("C3KX-NM-1G","4 x 1G SFP network module","3KX",XGuide,(_,_)=>Ports(4,"GigabitEthernet","SFP"));
        Add("C3KX-NM-10G","2 x SFP + 2 x SFP+ (paired modes)","3KX",XGuide,(_,_)=>new(){S(1,"GigabitEthernet","SFP",0),S(1,"TenGigabitEthernet","SFP+",1),S(3,"GigabitEthernet","SFP",2),S(2,"TenGigabitEthernet","SFP+",3)});
        Add("C3KX-NM-10GT","2 x 1/10G RJ-45 network module","3KX",XGuide,(_,_)=>Enumerable.Range(1,2).Select(n=>S(n,"TenGigabitEthernet","RJ-45",n)).ToList());
        Add("C3KX-SM-10G","2 x SFP+ service module","3KX",XGuide,(_,_)=>Enumerable.Range(1,2).Select(n=>S(n,"TenGigabitEthernet","SFP+",n+1)).ToList());
        Add("C3KX-NM-BLANK","Blank network module, no sockets","3KX",XGuide,(_,_)=>new());
        Add("C3850-NM-4-1G","4 x 1G SFP network module","3850",Guide3850,(_,_)=>Ports(4,"GigabitEthernet","SFP"));
        Add("C3850-NM-2-10G","2 x SFP + 2 x SFP+ network module","3850",Guide3850,(_,_)=>new(){S(1,"GigabitEthernet","SFP"),S(2,"GigabitEthernet","SFP"),S(3,"TenGigabitEthernet","SFP+"),S(4,"TenGigabitEthernet","SFP+")});
        Add("C3850-NM-4-10G","4 x 1/10G SFP+ network module","3850",Guide3850,(_,_)=>Ports(4,"TenGigabitEthernet","SFP+"));
        Add("C3850-NM-8-10G","8 x 1/10G SFP+ network module","3850",Guide3850,(_,_)=>Ports(8,"TenGigabitEthernet","SFP+"));
        Add("C3850-NM-2-40G","2 x 40G QSFP+ (native mode)","3850",Guide3850,(_,_)=>Ports(2,"FortyGigabitEthernet","QSFP+"));
        Add("C3850-NM-BLANK","Blank network module, no sockets","3850",Guide3850,(_,_)=>new());
        Add("C9200-NM-4G","4 x 1G SFP network module","9200",Guide9200,(_,_)=>Ports(4,"GigabitEthernet","SFP"));
        Add("C9200-NM-4X","4 x 1/10G SFP+ network module","9200",Guide9200,(_,_)=>Ports(4,"TenGigabitEthernet","SFP+"));
        Add("C9200-NM-BLANK","Blank network module, no sockets","9200",Guide9200,(_,_)=>new());
        Add("C9300-NM-4G","4 x 1G SFP network module","9300",Guide9300,(_,_)=>Ports(4,"GigabitEthernet","SFP"));
        Add("C9300-NM-4M","4 x 10G multigigabit RJ-45 network module","9300",Guide9300,(_,_)=>Ports(4,"TenGigabitEthernet","RJ-45"));
        Add("C9300-NM-2Q","2 x 40G QSFP+ (native mode)","9300",Guide9300,(_,_)=>Ports(2,"FortyGigabitEthernet","QSFP+"));
        Add("C9300-NM-8X","8 x 1/10G SFP+ network module","9300",Guide9300,(_,_)=>Ports(8,"TenGigabitEthernet","SFP+"));
        Add("C9300-NM-2Y","2 x 1/10/25G SFP28 network module","9300",Guide9300,(_,_)=>Enumerable.Range(1,2).Select(n=>S(n,"TwentyFiveGigE","SFP28",n)).ToList());
        Add("C9300-NM-BLANK","Blank network module, no sockets","9300",Guide9300,(_,_)=>new());
        Add("C9300X-NM-2C","2 x 40/100G QSFP28 (native mode)","9300X",Guide9300,(_,_)=>Ports(2,"HundredGigE","QSFP28"));
        Add("C9300X-NM-4C","4 x 40/100G QSFP28 (native mode)","9300X",Guide9300,(_,_)=>Enumerable.Range(1,4).Select(n=>S(n,"HundredGigE","QSFP28",(n-1)/2,(n-1)%2)).ToList());
        Add("C9300X-NM-8M","8 x 10G multigigabit RJ-45 network module","9300X",Guide9300,(_,_)=>Ports(8,"TenGigabitEthernet","RJ-45"));
        Add("C9300X-NM-8Y","8 x 1/10/25G SFP28 network module","9300X",Guide9300,(_,_)=>Ports(8,"TwentyFiveGigE","SFP28"));
        Add("C9300X-NM-BLANK","Blank network module, no sockets","9300X",Guide9300,(_,_)=>new());
        return result;
    }
    public static string CanonicalSku(string value)=>value.Trim().TrimEnd('=').ToUpperInvariant() is "C3KX-NM_BLNK" or "C3KX-NM-BLNK"?"C3KX-NM-BLANK":value.Trim().TrimEnd('=').ToUpperInvariant();
    static string Family(string sku)=>CiscoHardware.Is3560E48(sku)?"3560E":sku.StartsWith("WS-C3560X-")||sku.StartsWith("WS-C3750X-")?"3KX":sku.StartsWith("WS-C3850-")?"3850":sku.StartsWith("C9200-")?"9200":sku.StartsWith("C9300-")?"9300":sku.StartsWith("C9300X-")?"9300X":"";
    public static bool IsNetworkModulePid(string sku)=>Regex.IsMatch(sku,@"^(?:C(?:3KX|3850|9200|9300X?)[-_](?:NM|SM)|CVR-X2-|X2-10G)",RegexOptions.IgnoreCase);
    public static bool IsStandalone(string sku)=>CiscoHardware.Is3560E48(sku)||sku.StartsWith("WS-C3560X-",StringComparison.OrdinalIgnoreCase);
    public static bool IsModular(string sku)=>Family(sku).Length>0;
    static IEnumerable<Definition> Compatible(string model)
    {
        string family=Family(model);
        foreach(var d in definitions.Where(d=>d.Family==family||family=="9300"&&d.Family=="3850"&&!d.Sku.EndsWith("BLANK")))
        {
            if(family=="3850")
            {
                bool xs=model.EndsWith("XS"),large=model.StartsWith("WS-C3850-48"),xs24=model=="WS-C3850-24XS";
                if(xs&&(d.Sku is "C3850-NM-4-1G" or "C3850-NM-2-10G"))continue;
                if(d.Sku=="C3850-NM-4-10G"&&!large&&!xs)continue;
                if((d.Sku is "C3850-NM-8-10G" or "C3850-NM-2-40G")&&!xs24)continue;
            }
            if(family=="9300X"&&d.Sku=="C9300X-NM-4C"&&!new[]{"C9300X-24Y","C9300X-48HX","C9300X-48TX"}.Contains(model))continue;
            yield return d;
        }
    }
    public static IEnumerable<ModuleChoice> Choices=>definitions.Select(d=>new ModuleChoice{Sku=d.Sku,Name=d.Name,Source=d.Source,LayoutSource=d.Source});
    public static IReadOnlyList<CiscoHardware.Model> BaseModels {get;}=BuildBases();
    static List<CiscoHardware.Model> BuildBases()
    {
        var result=new List<CiscoHardware.Model>();
        void Add(string code,int count,string media,string source,string? interfaceKind=null)
        {
            string prefix=IsStandalone(code)?"0/":"{m}/0/",kind=interfaceKind??(media=="SFP+"?"TenGigabitEthernet":"GigabitEthernet");
            // The fiber 3750-X, 3850 and 9300X bases have one lower row.
            // C9300 SFP bases retain the two-row, twelve-port bank arrangement.
            bool single=media!="RJ-45"&&Family(code)!="9300";
            double leading=single?(code=="WS-C3750X-12S"?13:0):count==24?13:0;
            var sockets=Enumerable.Range(1,count).Select(n=>new CiscoHardware.Socket(n,leading+(single?n-1:(n-1)/2)+.5*((n-1)/(single?6:12)),single?1:(n-1)%2,media,kind+prefix+n,n.ToString(),kind=="TwentyFiveGigE"?new[]{"TwentyFiveGigabitEthernet"+prefix+n}:Array.Empty<string>())).ToList();
            // Only interfaces actually present in a backup are retained from this union.
            // No module is inferred from the CLI's unused software alternatives.
            var alternatives=Compatible(code).SelectMany(d=>Layout(d,code,"NM")).GroupBy(s=>s.Interface,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
            if(Family(code)=="3KX")foreach(int n in new[]{2,4})
                if(alternatives.All(s=>s.Interface!="GigabitEthernet"+(IsStandalone(code)?"1/":"{m}/1/")+n))alternatives.Add(new(n,n-1,0,"Unspecified","GigabitEthernet"+(IsStandalone(code)?"1/":"{m}/1/")+n,"Gi"+n,Array.Empty<string>()));
            // IOS retains otherwise-unused Gi/Te CLI stubs even on a base whose
            // compatible module list cannot activate every one of those names.
            if(Family(code)=="3850")foreach(string aliasKind in new[]{"GigabitEthernet","TenGigabitEthernet"})foreach(int n in Enumerable.Range(1,4))
                if(alternatives.All(s=>s.Interface!=aliasKind+"{m}/1/"+n))alternatives.Add(S(n,aliasKind,"Unspecified"));
            int index=0;foreach(var socket in alternatives)sockets.Add(socket with{Number=count+ ++index,Column=(index-1)/2,Row=(index-1)%2,Media="Unspecified",ConfiguredInterface=true});
            result.Add(new(code,code+"; optional uplink module",source,sockets));
        }
        foreach(string series in new[]{"3560X","3750X"})
        {
            foreach(int count in new[]{24,48})foreach(string suffix in new[]{"T","P","U"})Add("WS-C"+series+"-"+count+suffix,count,"RJ-45",XGuide);
            Add("WS-C"+series+"-48PF",48,"RJ-45",XGuide);
        }
        Add("WS-C3750X-12S",12,"SFP",XGuide);Add("WS-C3750X-24S",24,"SFP",XGuide);
        foreach(int count in new[]{24,48})foreach(string suffix in new[]{"T","P","U"})Add("WS-C3850-"+count+suffix,count,"RJ-45",Base3850);
        Add("WS-C3850-48F",48,"RJ-45",Base3850);
        foreach(int count in new[]{12,24}){Add("WS-C3850-"+count+"S",count,"SFP",Base3850);Add("WS-C3850-"+count+"XS",count,"SFP+",Base3850);}
        foreach(int count in new[]{24,48})foreach(string suffix in new[]{"T","P","PB"})Add("C9200-"+count+suffix,count,"RJ-45",Guide9200);
        Add("C9200-48PL",48,"RJ-45",Guide9200);
        foreach(int count in new[]{24,48})foreach(string suffix in new[]{"T","P","U","UB","H","S"})Add("C9300-"+count+suffix,count,suffix=="S"?"SFP":"RJ-45",Base9300);
        foreach(int count in new[]{12,24})Add("C9300X-"+count+"Y",count,"SFP28",Base9300,"TwentyFiveGigE");
        foreach(string suffix in new[]{"24HX","48HX","48TX"})Add("C9300X-"+suffix,int.Parse(suffix[..2]),"RJ-45",Base9300,"TenGigabitEthernet");
        return result;
    }
    static List<CiscoHardware.Socket> Layout(Definition definition,string model,string slot)
    {
        var sockets=definition.Layout(model,slot);
        if(model.StartsWith("WS-C3560X-"))sockets=sockets.Select(s=>s with{Interface=s.Interface.Replace("{m}/1/","1/")}).ToList();
        return sockets;
    }
    public static IEnumerable<ModuleSlotChoice> Slots(Inventory inv,DeviceIdentity parent)
    {
        if(inv.Vendor!="Cisco"||parent.ParentId.Length>0)yield break;
        string model=inv.Groups.FirstOrDefault(g=>g.Device.Equals(parent.Id,StringComparison.OrdinalIgnoreCase)&&g.Slot.Length==0&&g.Product.Length>0)?.Product??"";
        if(!IsModular(model))yield break;
        foreach(string slot in Family(model)=="3560E"?new[]{"X2-1","X2-2"}:new[]{"NM"})
        {
            var installed=inv.ModuleInventory?.FirstOrDefault(m=>m.Device.Equals(parent.Id,StringComparison.OrdinalIgnoreCase)&&m.Slot==slot);
            var edit=parent.ModuleSelections.FirstOrDefault(m=>m.Slot.Equals(slot,StringComparison.OrdinalIgnoreCase));
            yield return new ModuleSlotChoice{Slot=slot,Label=slot=="X2-1"?"Upper X2 bay":slot=="X2-2"?"Lower X2 bay":"Uplink network module",DetectedSku=installed is {OperatorSupplied:false}?installed.Sku:"",SelectedSku=edit?.Sku??(installed is {OperatorSupplied:true}?installed.Sku:""),Options=Compatible(model).Select(d=>new ModuleChoice{Sku=d.Sku,Name=d.Name,Source=d.Source,LayoutSource=d.Source}).ToList()};
        }
    }
    public static void Apply(Inventory inv,List<DeviceIdentity> rows)
    {
        if(inv.Vendor!="Cisco")return;
        var evidence=inv.ModuleInventory??new();
        foreach(var row in rows.Where(r=>r.ModuleSelections.Count>0))
        {
            var slots=Slots(inv,row).ToDictionary(s=>s.Slot,StringComparer.OrdinalIgnoreCase);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var choice in row.ModuleSelections)
            {
                string slot=choice.Slot.Trim().ToUpperInvariant(),sku=CanonicalSku(choice.Sku);
                if(sku.Length==0)continue;
                if(!seen.Add(slot)||!slots.TryGetValue(slot,out var allowed)||!allowed.Options.Any(o=>o.Sku==sku))throw new InvalidOperationException("Unsupported or duplicate Cisco module selection for member "+row.Id+", slot "+slot+".");
                var detected=evidence.FirstOrDefault(m=>m.Device==row.Id&&m.Slot==slot);
                if(detected is {OperatorSupplied:false}&&detected.Sku!=sku)throw new InvalidOperationException("Selected module conflicts with show inventory for member "+row.Id+", slot "+slot+".");
                if(detected==null)evidence.Add(new(){Device=row.Id,Slot=slot,Sku=sku,OperatorSupplied=true});
                else if(detected.OperatorSupplied)detected.Sku=sku;
            }
        }
        if(evidence.Count>0)inv.ModuleInventory=evidence;
        foreach(var item in evidence)
        {
            var baseGroup=inv.Groups.FirstOrDefault(g=>g.Device==item.Device&&g.Slot.Length==0&&IsModular(g.Product));
            if(baseGroup==null)throw new InvalidOperationException("Cisco module has no compatible base switch: "+item.Device+"/"+item.Slot+".");
            var definition=Compatible(baseGroup.Product).FirstOrDefault(d=>d.Sku==item.Sku)??throw new InvalidOperationException("Cisco module is incompatible with "+baseGroup.Product+": "+item.Sku+".");
            if(!(Family(baseGroup.Product)=="3560E"?new[]{"X2-1","X2-2"}:new[]{"NM"}).Contains(item.Slot))throw new InvalidOperationException("Cisco module slot is invalid: "+item.Slot+".");
            var sockets=Layout(definition,baseGroup.Product,item.Slot);
            string groupId=(ReportText.IsStack(inv)?"Member "+item.Device:"Switch")+" / "+item.Slot+" / "+item.Sku;
            var group=new Group{Id=groupId,Device=item.Device,Slot=item.Slot,Product=item.Sku,Copper=sockets.Count(s=>s.Media=="RJ-45"),Optical=sockets.Count(s=>s.Media!="RJ-45"),OpticalOnly=sockets.All(s=>s.Media!="RJ-45"),OpticalType=string.Join(" / ",sockets.Select(s=>s.Media).Distinct())};
            for(int i=0;i<sockets.Count;i++)
            {
                var socket=sockets[i];string id=CiscoHardware.Interface(socket,item.Device);
                var port=inv.Ports.FirstOrDefault(p=>CiscoHardware.Names(socket,item.Device).Contains(p.Id,StringComparer.OrdinalIgnoreCase));
                if(port==null){port=new(){Id=id};inv.Ports.Add(port);}
                port.Group=groupId;port.Number=i+1;port.DisplayNumber=socket.Label;port.Media=socket.Media;
                port.Notes.RemoveAll(n=>n.StartsWith("Configured uplink interface only;",StringComparison.Ordinal));
                port.Notes.Add("Installed module "+item.Sku+" in "+item.Slot+" establishes this socket. Saved interface settings do not establish live state or operational speed.");
                group.SocketLayout.Add(new(){Number=i+1,Column=socket.Column,Row=socket.Row,Media=socket.Media});
            }
            inv.Groups.RemoveAll(g=>g.Id==groupId);
            if(sockets.Count>0)inv.Groups.Add(group);
            inv.Assumptions.Add((item.OperatorSupplied?"Module supplied by the operator: ":"Module established by show inventory: ")+item.Device+"/"+item.Slot+"="+item.Sku+". Hardware reference: "+definition.Source);
        }
        if(evidence.Count>0)
        {
            inv.Assumptions.RemoveAll(a=>a.StartsWith("The 3560-E uplink table preserves",StringComparison.Ordinal));
            inv.Assumptions.Add("Selected or detected Cisco modules establish installed socket counts. Other saved uplink interfaces remain independent configured alternatives and do not add physical sockets; live state and active interface modes require confirmation.");
        }
        foreach(var group in inv.Groups.Where(g=>g.ConfiguredInterfaces&&evidence.Any(m=>m.Device==g.Device)).ToList())
        {
            var ports=inv.Ports.Where(p=>p.Group==group.Id).ToList();
            if(ports.Count==0){inv.Groups.Remove(group);continue;}
            if(!HasUnconfirmedModulesFor(inv,group.Device))
            {group.Id=(ReportText.IsStack(inv)?"Member "+group.Device:"Switch")+" / Retained uplink alternatives";foreach(var p in ports){p.Group=group.Id;p.Notes.RemoveAll(n=>n.StartsWith("Configured uplink interface only;",StringComparison.Ordinal));}}
            group.Copper=0;group.Optical=ports.Count;group.SocketLayout.Clear();
            for(int i=0;i<ports.Count;i++){ports[i].Number=i+1;group.SocketLayout.Add(new(){Number=i+1,Column=i/2,Row=i%2,Media="Unspecified"});}
            if(evidence.Any(m=>m.Device==group.Device))foreach(var p in ports)p.Notes.Add("Retained configured uplink alternative. This entry is not an additional installed socket; module/interface mode must be checked before use.");
        }
        inv.Groups=inv.Groups.OrderBy(g=>int.TryParse(g.Device,out int number)?number:int.MaxValue).ThenBy(g=>g.ConfiguredInterfaces).ThenBy(g=>g.Slot.Length==0?0:1).ThenBy(g=>g.Slot,StringComparer.OrdinalIgnoreCase).ToList();
        // A sparse backup may already contain the last module interface while
        // selection introduces earlier sockets. Normalize order after moving
        // existing records so reports keep physical numbering and all settings.
        var groupOrder=inv.Groups.Select((g,i)=>(g.Id,i)).ToDictionary(p=>p.Id,p=>p.i);
        inv.Ports=inv.Ports.OrderBy(p=>groupOrder.GetValueOrDefault(p.Group,int.MaxValue)).ThenBy(p=>p.Number).ToList();
        inv.Assumptions=inv.Assumptions.Distinct().ToList();
        foreach(var port in inv.Ports)port.Notes=port.Notes.Distinct().ToList();
    }
    static bool HasUnconfirmedModulesFor(Inventory inv,string device)=>inv.Groups.Where(g=>g.Device==device&&g.Slot.Length==0&&IsModular(g.Product)).Any(g=>(Family(g.Product)=="3560E"?new[]{"X2-1","X2-2"}:new[]{"NM"}).Any(slot=>!(inv.ModuleInventory?.Any(m=>m.Device==g.Device&&m.Slot==slot)??false)));
    public static bool HasUnconfirmedModules(Inventory inv)=>inv.Groups.Where(g=>g.Slot.Length==0&&IsModular(g.Product)).Any(g=>HasUnconfirmedModulesFor(inv,g.Device));
    public static void ApplyDetected(Inventory inv,string source)
    {
        source=CiscoParser.EvidenceText(source.Replace("\r",""));
        var installed=new List<InstalledModule>();
        foreach(Match record in Regex.Matches(source,@"(?im)^[ \t]*NAME:\s*""([^""]+)""[^\n]*\n[ \t]*PID:\s*([\w-]+)\s*,"))
        {
            string sku=CanonicalSku(record.Groups[2].Value),name=record.Groups[1].Value;
            var definition=definitions.FirstOrDefault(d=>d.Sku==sku);
            if(definition==null)
            {
                if(IsNetworkModulePid(sku))inv.Blockers.Add("Unsupported Cisco network module PID in show inventory: "+sku+". A manual module selection cannot replace explicit inventory evidence.");
                continue;
            }
            string device="",slot="";
            if(definition.Family=="3560E")
            {
                var bay=Regex.Match(name,@"(?:X2[- ]?(?:Slot\s+)?|TenGigabitEthernet0/|FRU Uplink Module\s+)([12])\b",RegexOptions.IgnoreCase);
                if(bay.Success){device="1";slot="X2-"+bay.Groups[1].Value;}
                else if(Regex.Match(name,@"GigabitEthernet0/(49|50|51|52)\b",RegexOptions.IgnoreCase) is {Success:true} gig){device="1";slot=int.Parse(gig.Groups[1].Value)<51?"X2-1":"X2-2";}
            }
            else
            {
                var member=Regex.Match(name,@"\bSwitch\s+(\d+)\b",RegexOptions.IgnoreCase);
                if(member.Success){device=member.Groups[1].Value;slot="NM";}
                else if(inv.Groups.Count(g=>g.Slot.Length==0&&IsModular(g.Product))==1){device=inv.Groups.First(g=>g.Slot.Length==0&&IsModular(g.Product)).Device;slot="NM";}
            }
            if(device.Length==0){inv.Blockers.Add("Show inventory module PID cannot be assigned to a verified member/bay: "+sku+".");continue;}
            var prior=installed.FirstOrDefault(m=>m.Device==device&&m.Slot==slot);
            if(prior!=null&&prior.Sku!=sku)inv.Blockers.Add("Conflicting Cisco module PIDs for "+device+"/"+slot+".");
            else if(prior==null)installed.Add(new(){Device=device,Slot=slot,Sku=sku});
        }
        if(installed.Count==0)return;inv.ModuleInventory=installed;
        try{Apply(inv,new());}catch(InvalidOperationException error){inv.Blockers.Add(error.Message);}
    }
}
