using SwitchportGenerator;
using System.Globalization;

static class PositionChecks
{
    static int assertions,diagrams;
    static readonly List<string> audit=new(){"Model,Installed module,Physical sockets,Final diagram verified,Hardware reference"};
    static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static string Fixture(string sku)
    {
        if(HardwareProfiles.ChassisModels.ContainsKey(sku))return "; "+sku+" Configuration Editor\nmodule A type "+HardwareCatalog.Modules.First(m=>HardwareCatalog.IsCompatibleModule(m.Sku,sku)).Sku+"\n";
        var m=HardwareCatalog.Find(sku);
        return CiscoHardware.Find(sku)!=null?"version 17.12\nhostname POSITION-LAB\nswitch 1 provision "+sku+"\n":MikroTikParser.Models.Any(x=>x.Code==sku)?"# RouterOS 7.16\n# model = "+sku+"\n/system identity\nset name=POSITION-LAB\n":m?.OS=="Comware"?"# "+sku+"\nversion 7.1.070\nsysname POSITION-LAB\n":m?.OS=="Instant On 1960"||sku.StartsWith("JL68")?"config-file-header\nPOSITION-LAB\nvInstantOn_"+(sku.StartsWith("JL68")?"1930":"1960")+"_3.3.0.0\nSKU Description \"Instant On "+sku+"\"\n":m?.OS=="AOS-CX"?"! AOS-CX "+sku+"\n":"; "+sku+" Configuration Editor\n";
    }
    static Inventory Parse(string sku)=>DeviceModels.Parse(Fixture(sku),Array.Empty<DeviceIdentity>());
    static string Source(string sku)=>HardwareCatalog.Find(sku)?.LayoutSource??CiscoHardware.Find(sku)?.Source??CiscoModuleCatalog.Choices.FirstOrDefault(m=>m.Sku==sku)?.LayoutSource??(MikroTikParser.Models.Any(m=>m.Code==sku)?MikroTikParser.ModelSource(sku):"docs/PHYSICAL_POSITION_AUDIT.md");
    static QuickSheet.Result Final(Inventory inv,string model,string module="")
    {
        Check(inv.Blockers.Count==0,model+": "+string.Join(';',inv.Blockers));
        var original=Charts.Layout(inv);Check(Charts.Validate(inv,original).Count==0,model+": reference geometry");
        QuickSheet.Result sheet;try{sheet=QuickSheet.Build(inv,original,"POSITION-AUDIT");}catch(Exception e){throw new Exception(model+" / "+module+": "+e.Message,e);}diagrams++;
        Check(sheet.Sockets.Count==original.Count,model+": every physical socket is retained");
        Check(Charts.ValidateScene(sheet.Scene).Count==0,model+": finished diagram fits without collisions");
        Check(sheet.Sockets.Select(s=>(s.Port,s.Suffix)).Distinct().Count()==original.Count,model+": unique physical identities");
        var expected=original.ToDictionary(p=>(p.Port,p.Socket));
        foreach(var group in inv.Groups)
        {
            var sockets=sheet.Sockets.Where(s=>inv.Ports.Single(p=>p.Id==s.Port).Group==group.Id).ToArray();
            // Inspect the final rendered positions, not merely the source profile.
            // Empty rows and bank gaps must not be removed by coordinate ranking.
            foreach(var a in sockets)foreach(var b in sockets)
            {
                var ea=expected[(a.Port,a.Suffix)].Tile;var eb=expected[(b.Port,b.Suffix)].Tile;
                Check(Math.Sign(a.Tile.X-b.Tile.X)==Math.Sign(ea.X-eb.X),model+": left/right order "+a.Port+"/"+b.Port);
                Check(Math.Sign(Math.Round(a.Tile.Y-b.Tile.Y,5))==Math.Sign(Math.Round(ea.Y-eb.Y,5)),model+": upper/lower order "+a.Port+"/"+b.Port);
            }
            foreach(var row in sockets.GroupBy(s=>Math.Round(s.Tile.Y,5)))
            {
                var ordered=row.OrderBy(s=>s.Tile.X).ToArray();
                var pitch=ordered.Zip(ordered.Skip(1)).Where(p=>Math.Abs(expected[(p.Second.Port,p.Second.Suffix)].Tile.X-expected[(p.First.Port,p.First.Suffix)].Tile.X-48)<.001).Select(p=>p.Second.Tile.X-p.First.Tile.X).DefaultIfEmpty(0).Min();
                if(pitch==0)continue;
                foreach(var pair in ordered.Zip(ordered.Skip(1)))
                    if(expected[(pair.Second.Port,pair.Second.Suffix)].Tile.X-expected[(pair.First.Port,pair.First.Suffix)].Tile.X>48.1)
                        Check(pair.Second.Tile.X-pair.First.Tile.X>pitch+.01,model+": physical bank boundary survives fitting");
            }
        }
        foreach(var baseGroup in inv.Groups.Where(g=>g.Slot.Length==0&&CiscoModuleCatalog.IsModular(g.Product)))
        {
            var basis=sheet.Sockets.Where(s=>inv.Ports.Single(p=>p.Id==s.Port).Group==baseGroup.Id).ToArray();
            foreach(var g in inv.Groups.Where(g=>g.Device==baseGroup.Device&&g.Slot.Length>0&&!g.ConfiguredInterfaces))
            {
                var moduleSockets=sheet.Sockets.Where(s=>inv.Ports.Single(p=>p.Id==s.Port).Group==g.Id).ToArray();
                Check(moduleSockets.Where(s=>!s.Optical).All(s=>int.TryParse(s.Display,out _)),model+": copper module uses readable physical socket numbers");
                Check(moduleSockets.Min(s=>s.Tile.X)>basis.Max(s=>s.Tile.X),model+": module occupies right-hand bay");
                Check(moduleSockets.Min(s=>s.Tile.Y)>=basis.Min(s=>s.Tile.Y)-(QuickSheet.PortHeight+QuickSheet.PortGap)*sheet.GeometryScale-.01&&moduleSockets.Max(s=>s.Tile.Y)<=basis.Max(s=>s.Tile.Y)+.01,model+": installed module stays on its member face");
            }
        }
        string Q(string x)=>"\""+x.Replace("\"","\"\"")+"\"";
        audit.Add(string.Join(',',new[]{model,module,sheet.Sockets.Count.ToString(CultureInfo.InvariantCulture),"PASS",Source(module.Length>0?module:model)}.Select(Q)));
        return sheet;
    }
    static void References()
    {
        // Literal front-panel relationships transcribed from the drawings listed
        // in docs/PHYSICAL_POSITION_AUDIT.md. These are independent of builders.
        QuickSheet.Result Map(string sku)=>Final(Parse(sku),sku);
        QuickSheet.Socket Port(QuickSheet.Result s,string label)=>s.Sockets.FirstOrDefault(p=>p.Display+p.Suffix==label)??throw new Exception("Missing reference label "+label+" in "+string.Join(',',s.Sockets.Select(p=>p.Display)));
        void Lower(QuickSheet.Result s,string a,string b)=>Check(Port(s,a).Tile.Y>Port(s,b).Tile.Y,a+" is below "+b);
        void Right(QuickSheet.Result s,string a,string b)=>Check(Port(s,a).Tile.X>Port(s,b).Tile.X,a+" is right of "+b);
        void SameRow(QuickSheet.Result s,params string[] labels)=>Check(labels.Select(l=>Math.Round(Port(s,l).Tile.Y,5)).Distinct().Count()==1,"Reference row: "+string.Join(',',labels));
        foreach(string sku in CiscoHardware.Models.Where(m=>m.Code.StartsWith("WS-C3650-")).Select(m=>m.Code))
        {var s=Map(sku);SameRow(s,"2","U1","U2","U3","U4");Lower(s,"U1","1");Right(s,"U1",sku.Contains("-24")?"24":"48");}
        foreach(string sku in new[]{"WS-C3750X-12S","WS-C3750X-24S","WS-C3850-12S","WS-C3850-24S","WS-C3850-12XS","WS-C3850-24XS","C9300X-12Y","C9300X-24Y"})
        {var s=Map(sku);Check(s.Sockets.Select(p=>p.Tile.Y).Distinct().Count()==1,sku+": single fiber row");Right(s,"12","1");}
        foreach(string sku in new[]{"C9300-24S","C9300-48S"}){var s=Map(sku);SameRow(s,"1","3","5");Lower(s,"2","1");}
        foreach(string sku in new[]{"C9500-24Y4C","C9500-48Y4C"})
        {var s=Map(sku);int n=sku.Contains("24Y")?24:48;Lower(s,(n+2).ToString(),(n+1).ToString());Right(s,(n+3).ToString(),(n+1).ToString());if(n==24)Check(Port(s,"25").Tile.X-Port(s,"24").Tile.X>10*Port(s,"1").Tile.W,"9500-24Y4C retains interior blank fascia");}
        foreach(string sku in new[]{"CRS326-24S+2Q+RM","CRS328-24P-4S+RM","CRS326-24G-2S+RM","CRS326-24G-2S+IN"})
        {var s=Map(sku);Lower(s,"1","2");if(sku.Contains("24S")){Lower(s,"Q1","Q2");SameRow(s,"1","Q1","E1");}else if(sku.Contains("328")){Lower(s,"S1","S2");SameRow(s,"1","S1");}else SameRow(s,"1","S1","S2");}
        foreach(string sku in new[]{"CRS317-1G-16S+RM","CRS309-1G-8S+IN"}){var s=Map(sku);Right(s,"E1",sku.Contains("317")?"16":"8");}
        {var s=Map("CRS305-1G-4S+IN");Right(s,"1","E1");}
        foreach(string sku in new[]{"J9779A","J9782A"}){var s=Map(sku);SameRow(s,"2","25","26","27","28");Right(s,"27","26");}
        foreach(string sku in new[]{"J9774A","J9777A","J9780A","J9783A"}){var s=Map(sku);SameRow(s,"2","9T","10T","9M","10M");Right(s,"10T","9T");Right(s,"9M","10T");Right(s,"10M","9M");}
        foreach(string sku in new[]{"J9538A","J9546A","J9993A","J9995A","J9996A","J8707A","J8708A","J4821A","J4878A","J4852A"})
        {var m=HardwareCatalog.Find(sku)!;Check(m.Sockets.All(p=>p.Row==1),sku+": lower module row retained");}
        foreach(string sku in new[]{"JL558A","JL256A","JL675A","R8N85A"})
        {var inv=Parse(sku);var s=Final(inv,sku);var copper=s.Sockets.Where(p=>inv.Ports.Single(x=>x.Id==p.Port).Media=="RJ-45");var fiber=s.Sockets.Except(copper);Check(fiber.Max(p=>p.Tile.X)<copper.Min(p=>p.Tile.X),sku+": optics to left");}
        foreach(string sku in new[]{"JL479A","JL581A"})
        {var m=HardwareCatalog.Find(sku);if(m==null)continue;Check(m.Sockets.Where(s=>s.Number>48).Select(s=>s.Row).Distinct().Count()==3,sku+": six QSFP sockets in three rows");}
        foreach(string sku in new[]{"J4820A","J4820B","J8161A"})
        {var m=HardwareCatalog.Find(sku)!;Check(m.Sockets.Where(s=>s.Row==0).OrderBy(s=>s.Column).Select(s=>s.Number).SequenceEqual(new[]{1,2,3,4,5,6,13,14,15,16,17,18}),sku+": XL row-block numbering");}
        var rear=DeviceModels.Parse(Fixture("J9726A"),new[]{new DeviceIdentity{Id="1",ModuleSelections=new(){new(){Slot="A",Sku="J9731A"},new(){Slot="B",Sku="J9732A"}}}});
        var rearSheet=Final(rear,"J9726A","J9731A + J9732A");SameRow(rearSheet,"A1","A2","B1","B2");Right(rearSheet,"B1","A2");Lower(rearSheet,"A1","1");
    }
    static int Main(string[] args)
    {
        try
        {
            References();var modules=new HashSet<string>();
            foreach(var choice in DeviceModels.Choices)
            {
                var inv=Parse(choice.Sku);Final(inv,choice.Sku);
                var parent=inv.Devices.FirstOrDefault(d=>d.ParentId.Length==0);if(parent==null)continue;
                foreach(var slot in DeviceModules.Slots(inv,parent))foreach(var option in slot.Options)
                {
                    if(!modules.Add(option.Sku))continue;
                    string selectionSource=Fixture(choice.Sku);if(HardwareProfiles.ChassisModels.ContainsKey(choice.Sku))selectionSource=string.Join('\n',selectionSource.Split('\n').Where(l=>!l.StartsWith("module ")));
                    var selected=DeviceModels.Parse(selectionSource,new[]{new DeviceIdentity{Id=parent.Id,ModuleSelections=new(){new(){Slot=slot.Slot,Sku=option.Sku}}}});
                    Final(selected,choice.Sku,option.Sku);
                }
            }
            foreach(string sku in new[]{"JL680A","JL681A","JL682A","JL683A","JL683B","JL684A","JL684B","JL685A","JL686A","JL686B"})Final(Parse(sku),sku);
            Check(modules.Count==75,"Every enabled module is exercised in a finished report");
            string root=Path.GetFullPath(args.FirstOrDefault()??"position-fixtures");Directory.CreateDirectory(root);
            File.WriteAllLines(Path.Combine(root,"position-audit.csv"),audit);
            foreach(string sku in new[]{"WS-C3650-48FD","WS-C3850-24XS","C9500-24Y4C","CRS326-24S+2Q+RM","CRS317-1G-16S+RM","J9779A","JL805A"})
            {
                string source=Path.Combine(root,sku.Replace('+','_')+".cfg");File.WriteAllText(source,Fixture(sku));
                Check(GeneratorEntry.Run(new[]{"--input",source,"--output",Path.Combine(root,sku.Replace('+','_'))})==0,"Actual PDF/PNG/SVG export: "+sku);
            }
            Console.WriteLine($"PASS {assertions} physical-position assertions across {diagrams} finished diagrams; {modules.Count} module PIDs.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
