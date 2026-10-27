using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SwitchportGenerator;
public static class GeneratorEntry
{
    static readonly string[] Visuals={"wall-chart.png","a4-one-page.png","a4-one-page.pdf","a4-booklet.pdf"};
    static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static int Main(string[] args)=>Run(args);
    public static int Run(string[] args,RenderPalette? renderPalette=null)
    {
        string? input=null,output=null,models=null,modelName=null,logo=null,deviceDetails=null,paletteFile=null;bool namedFolder=false;
        for(int i=0;i<args.Length;i++)
        {
            if(args[i]=="--input"&&i+1<args.Length)input=args[++i];
            else if(args[i]=="--output"&&i+1<args.Length)output=args[++i];
            else if(args[i]=="--models"&&i+1<args.Length)models=args[++i];
            else if(args[i]=="--model-name"&&i+1<args.Length)modelName=args[++i];
            else if(args[i]=="--logo"&&i+1<args.Length)logo=args[++i];
            else if(args[i]=="--device-details"&&i+1<args.Length)deviceDetails=args[++i];
            else if(args[i]=="--palette"&&i+1<args.Length)paletteFile=args[++i];
            else if(args[i]=="--named-folder")namedFolder=true;
            else {Console.Error.WriteLine("Usage: SwitchportGenerator.exe --input <configuration-file> --output <output-directory> [--models <SKU or member=SKU pairs>] [--model-name <printed model name>] [--logo <image-file>] [--device-details <JSON-file>]");return 2;}
        }
        if(string.IsNullOrWhiteSpace(input)||string.IsNullOrWhiteSpace(output)){Console.Error.WriteLine("Both --input and --output are required.");return 2;}
        try
        {
            input=Path.GetFullPath(input);output=Path.GetFullPath(output);
            // Each run uses an empty directory so a blocked run cannot leave stale charts.
            Directory.CreateDirectory(output);
            if(!namedFolder&&Directory.EnumerateFileSystemEntries(output).Any()){Console.Error.WriteLine("Output directory must be empty. Existing files were not modified.");return 2;}
            ReportLogo? reportLogo=logo==null?null:ReportLogo.Load(Path.GetFullPath(logo));
            Inventory inv;
            if(!File.Exists(input))inv=new Inventory{Blockers=new(){"Input file does not exist."}};
            else if(new FileInfo(input).Length>20_000_000)inv=new Inventory{Blockers=new(){"Input exceeds the 20 MB limit."}};
            else
            {
                string source;
                try{source=ConfigInput.Read(input);}
                catch(InvalidOperationException ex){source="";inv=new Inventory{Blockers=new(){ex.Message}};File.WriteAllText(Path.Combine(output,"validation-report.txt"),"RENDER_BLOCKED=TRUE\n"+ex.Message);Console.Error.WriteLine("RENDER_BLOCKED=TRUE\n"+ex.Message);return 3;}
                try{inv=DeviceModels.Parse(source,deviceDetails==null?Array.Empty<DeviceIdentity>():DeviceIdentities.Read(Path.GetFullPath(deviceDetails)),models);}
                catch(InvalidOperationException ex){inv=Parser.Parse(source,models);DeviceIdentities.Apply(inv,Array.Empty<DeviceIdentity>());inv.Blockers.Add(ex.Message);}
            }
            if(!string.IsNullOrWhiteSpace(modelName))
            {
                inv.ReportModel=Parser.Safe(modelName);
                if(inv.ReportModel.Length>100)inv.Blockers.Add("The model name must be 100 characters or fewer.");
                inv.Assumptions.Add("Report model name supplied by the operator: "+inv.ReportModel+". Hardware layout validation still uses the configured or explicitly selected inventory.");
            }
            foreach(var group in inv.Groups)
            {
                var catalogue=HardwareCatalog.Find(group.Product);
                if(catalogue!=null)inv.Assumptions.Add("Hardware evidence: "+group.Product+" | "+catalogue.LayoutSource);
            }
            if(namedFolder)
            {
                output=ReportNaming.AvailableDirectory(output,inv);Directory.CreateDirectory(output);
                Console.WriteLine("OUTPUT_DIRECTORY="+output);
            }
            if(paletteFile!=null)
            {
                if(new FileInfo(paletteFile).Length>4_000_000)throw new InvalidOperationException("Workspace palette exceeds the supported size.");
                renderPalette=RenderPalette.Deserialize(File.ReadAllText(paletteFile));
            }
            renderPalette?.Apply(inv);
            var placements=Charts.Layout(inv);inv.Blockers.AddRange(Charts.Validate(inv,placements));inv.Blockers=inv.Blockers.Distinct().ToList();
            string json=JsonSerializer.Serialize(inv,JsonOptions);byte[] jsonBytes=new UTF8Encoding(false).GetBytes(json);string hash=Hash(jsonBytes);
            File.WriteAllBytes(Path.Combine(output,"inventory.json"),jsonBytes);
            var csv=new StringBuilder();csv.AppendLine("Port,Description,Mode,Untagged VLAN,Untagged VLAN Name,Tagged VLANs,Voice VLAN,Trunk/LAG,Connected Device,Media,Configured Speed,Admin State,Ingress PVID,Configured Addresses,Aggregation Mode,Device ID,Device Name,Serial Number,Asset Tag,Device Details,Module ID,Module Name,Module Serial Number,Module Asset Tag,Module Details,Device Model,Module Model,Logical interfaces");
            foreach(var p in inv.Ports)
            {
                var group=inv.Groups.FirstOrDefault(g=>g.Id==p.Group);string deviceId=group?.Device??"";var device=DeviceIdentities.For(inv,deviceId);
                string moduleId=group==null?"":DeviceIdentities.ModuleId(group);var module=DeviceIdentities.For(inv,moduleId);
                string native=p.UntaggedVlan?.ToString()??p.UntaggedState;
                string vlanName=inv.Vlans.FirstOrDefault(v=>v.Id==p.UntaggedVlan)?.Name??"";
                string NoteValues(string prefix)=>string.Join(';',p.Notes.Where(n=>n.StartsWith(prefix,StringComparison.Ordinal)).Select(n=>n[prefix.Length..]).Distinct());
                string tagged=p.Notes.Any(n=>n.StartsWith("Tagged VLANs:",StringComparison.Ordinal))?"All":string.Join(';',p.TaggedVlans);
                string connected=p.ConnectedDevice=="Unknown/Confirm before use"?"":p.ConnectedDevice;
                csv.AppendLine(string.Join(',',new[]{p.Id,p.Description,p.Mode,native,vlanName,tagged,p.VoiceVlan?.ToString()??"",p.Aggregate,connected,p.Media,p.Speed,p.Disabled.HasValue?p.Disabled.Value?"Disabled":"Enabled":"Unknown",NoteValues("Configured ingress PVID: "),NoteValues("Address: "),NoteValues("Aggregation mode: "),deviceId,device?.Name??"",device?.SerialNumber??"",device?.AssetTag??"",device?.Notes??"",moduleId,module?.Name??"",module?.SerialNumber??"",module?.AssetTag??"",module?.Notes??"",device?.Model??"",module?.Model??"",string.Join(';',p.RelatedInterfaces)}.Select(Quote)));
            }
            File.WriteAllText(Path.Combine(output,"port-map.csv"),csv.ToString(),new UTF8Encoding(true));
            string summary=Summary(inv);
            File.WriteAllText(Path.Combine(output,"technician-summary.txt"),summary);
            if(inv.Blockers.Count>0){WriteReport(output,inv,hash,false);Console.Error.WriteLine("RENDER_BLOCKED=TRUE\n"+string.Join('\n',inv.Blockers));return 3;}
            string staging=Path.Combine(output,".rendering");Directory.CreateDirectory(staging);
            try
            {
                var scene=Charts.Create(inv,placements,hash);
                QuickSheet.FitToPage(scene);
                var booklet=Charts.Booklet(inv,scene,hash);
                if(reportLogo!=null)foreach(var page in booklet)reportLogo.Apply(page);
                var sceneErrors=booklet.SelectMany(Charts.ValidateScene).Distinct().ToList();
                if(sceneErrors.Count>0)
                {
                    inv.Blockers.AddRange(sceneErrors);WriteReport(output,inv,hash,false);
                    Console.Error.WriteLine("RENDER_BLOCKED=TRUE\n"+string.Join('\n',sceneErrors));return 3;
                }
                // Both raster diagrams use the same A4 landscape canvas at 300 DPI.
                Png.Save(scene,Path.Combine(staging,Visuals[0]),3508,2480,true);
                Png.Save(scene,Path.Combine(staging,Visuals[1]),3508,2480,true);
                Pdf.Save(new(){scene},Path.Combine(staging,Visuals[2]));
                Pdf.Save(booklet,Path.Combine(staging,Visuals[3]));
                foreach(string file in Visuals){if(new FileInfo(Path.Combine(staging,file)).Length<100)throw new InvalidOperationException("Generated chart is empty.");}
                foreach(string file in Visuals)File.Move(Path.Combine(staging,file),Path.Combine(output,file));
                WriteReport(output,inv,hash,true,scene);
                Console.WriteLine("Created 8 deliverables. Physical interfaces: "+inv.Ports.Count+". Inventory SHA256: "+hash);return 0;
            }
            catch
            {
                foreach(string file in Visuals)File.Delete(Path.Combine(output,file));throw;
            }
            finally {if(Directory.Exists(staging))Directory.Delete(staging,true);}
        }
        catch(OnePageCapacityException ex)
        {
            if(output!=null&&Directory.Exists(output))File.WriteAllText(Path.Combine(output,"validation-report.txt"),"RENDER_BLOCKED=TRUE\n"+ex.Message+"\n");
            Console.Error.WriteLine("RENDER_BLOCKED=TRUE\n"+ex.Message);return 3;
        }
        catch(Exception ex)
        {
            // Never write raw input or an exception stack containing configuration data.
            if(output!=null&&Directory.Exists(output))
            {
                try{File.WriteAllText(Path.Combine(output,"validation-report.txt"),"RENDER_BLOCKED=TRUE\nGeneration failed: "+ex.GetType().Name+". Check file permissions, configuration syntax, and resource limits.\n");}catch{}
            }
            Console.Error.WriteLine("RENDER_BLOCKED=TRUE\nGeneration failed: "+ex.GetType().Name+". Check the validation report and file permissions.");return 4;
        }
    }
    static string Summary(Inventory inv)
    {
        var s=new StringBuilder();
        s.AppendLine("NETWORK SUMMARY");s.AppendLine(new string('=',72));
        s.AppendLine("Device:       "+inv.Hostname);s.AppendLine("Platform:     "+inv.OS);
        s.AppendLine("Firmware:     "+inv.Firmware);s.AppendLine("Topology:     "+inv.Topology);
        string model=ReportText.SwitchModel(inv);if(model.Length>0)s.AppendLine("Model:        "+model);
        s.AppendLine("Physical:     "+inv.Ports.Count+" ports across "+inv.Groups.Count+" inventory groups");
        s.AppendLine("Stack links:  "+inv.Ports.Count(p=>p.StackLink));
        s.AppendLine("Aggregations: "+inv.Ports.Where(p=>p.Aggregate.Length>0).Select(p=>p.Aggregate).Distinct().Count());
        void Section(string name,IEnumerable<string> items)
        {
            s.AppendLine();s.AppendLine(name.ToUpperInvariant());s.AppendLine(new string('-',72));
            foreach(string line in items)s.AppendLine("  "+line);
        }
        if(inv.Devices.Any(d=>d.HasDetails))Section("Device details",inv.Devices.Where(d=>d.HasDetails).Select(d=>string.Join(" | ",new[]{d.Label,d.Model,d.Name.Length>0?"Name: "+d.Name:"",d.SerialNumber.Length>0?"S/N: "+d.SerialNumber:"",d.AssetTag.Length>0?"Asset: "+d.AssetTag:"",d.Notes.Length>0?d.Notes:""}.Where(s=>s.Length>0))));
        Section("Physical inventory",inv.Groups.Select(g=>string.Join("  |  ",new[]{ReportText.GroupLabel(inv,g),ReportText.ModelName(g.Product),g.Expected+" ports"}.Where(s=>s.Length>0))));
        if(inv.SVIs.Count>0)Section("Layer 3 interfaces",inv.SVIs.Select(v=>v.Interface+"  |  "+v.Address));
        if(inv.Routes.Count>0)Section("Routes",inv.Routes);
        if(inv.Management.Count>0)Section("Management",inv.Management);
        if(inv.Mirrors.Count>0)Section("Mirrors",inv.Mirrors);
        s.AppendLine();s.AppendLine(ReportText.CableMoveDisclaimer);
        return s.ToString();
    }
    static string Quote(string value)=>"\""+Parser.Safe(value).Replace("\"","\"\"")+"\"";
    static void WriteReport(string output,Inventory inv,string hash,bool success,Scene? scene=null)
    {
        var report=new StringBuilder();report.AppendLine("VALIDATION REPORT");report.AppendLine(new string('=',72));report.AppendLine("RENDER_BLOCKED="+(success?"FALSE":"TRUE"));
        report.AppendLine("Inventory SHA256: "+hash);report.AppendLine("Expected physical count: "+inv.Groups.Sum(g=>g.Expected));report.AppendLine("Generated physical count: "+inv.Ports.Count);
        report.AppendLine("Count delta: "+(inv.Ports.Count-inv.Groups.Sum(g=>g.Expected)));
        report.AppendLine("CSV data row count: "+inv.Ports.Count);
        report.AppendLine("Geometry: physical rows and port-bank placement follow the identified hardware; single-row modules retain their row.");
        report.AppendLine("Odd/even row, bounds, uniqueness, ordering, non-overlap, centering, spacing, text/port/marker collision, and marker clearance checks: "+(success?"PASS":"BLOCKED"));
        report.AppendLine($"Working page: interface reference faces are {QuickSheet.PortWidth:0.###} x {QuickSheet.PortHeight:0.###} pt for RJ45 and {QuickSheet.PortWidth:0.###} x {QuickSheet.OpticalHeight:0.###} pt for optical cages. One geometry factor preserves both aspect ratios across all interfaces. The solver first uses normal spacing, then tighter padding and readable role-wide typography. Port number labels stay at least {QuickSheet.MinimumPortFont:0.###} printed pt; key/panel/body text stays at least {QuickSheet.MinimumBodyFont:0.###} printed pt. Preferred VLAN circles and voice diamonds scale with the interface geometry from a {2*QuickSheet.DotRadius:0.###} pt reference diameter, capped at {2*QuickSheet.MaximumDotRadius:0.###} printed pt. The VLAN key reserves a readable text column before bank enlargement, and body/key/panel typography has printed upper bounds. If proportional growth and compaction cannot fit them, one global marker size is reduced only as much as needed, including guide and VLAN-key symbols. Markers are never omitted, and PDF inventory lists still wrap in full.");
        if(scene!=null)report.AppendLine($"Adaptive sizing: interface geometry {scene.DiagramScale:0.###}x; printed marker diameter {2*scene.MarkerRadius:0.###} pt; compaction level {scene.CompactionLevel}. Interface faces are never stretched independently in width or height. FitToPage leaves the solved layout unchanged.");
        if(success)
        {
            var palette=VlanPalette.Assign(inv);
            var entries=palette.OrderBy(p=>p.Key).ToList();
            report.AppendLine("PDF inventory: tagged VLAN values wrap in full; oversized rows continue on additional pages with their port identity. Native color samples match the diagram palette.");
            report.AppendLine("VLAN colors: perceptual separation is prioritized within a soft-color palette; crowded palettes use the best available separation rather than repeating colors.");
            if(entries.Count>1)
            {
                double minimum=double.MaxValue;string pair="";
                for(int i=0;i<entries.Count;i++)for(int j=0;j<i;j++)
                {
                    double distance=PerceptualColor.Difference(entries[i].Value,entries[j].Value);
                    if(distance<minimum){minimum=distance;pair=entries[j].Key+" / "+entries[i].Key;}
                }
                report.AppendLine($"Closest VLAN color pair: {pair}; CIEDE2000 difference {minimum:0.###}.");
            }
            foreach(var item in entries)report.AppendLine($"VLAN {item.Key}: {item.Value}");
            foreach(var item in AggregatePalette.Assign(inv,p=>VlanPalette.NativeFill(p,palette),palette.Values))report.AppendLine($"Outline {item.Key}: {item.Value}");
        }
        report.AppendLine("All raster diagrams: A4 landscape, 3508 x 2480 pixels at 300 DPI. All PDF pages: A4 landscape.");
        report.AppendLine("One complete working diagram fits A4 without needing supplemental pages for the port key. One normalized inventory feeds CSV, summary, JSON, both PNGs, and both PDFs. Inventory fingerprint is retained in this validation report.");
        report.AppendLine(ReportText.CableMoveDisclaimer);
        report.AppendLine("Assumptions/limitations:");foreach(var a in inv.Assumptions)report.AppendLine("- "+a);
        foreach(var b in inv.Blockers)report.AppendLine("BLOCKER: "+b);
        report.AppendLine("Output file SHA256:");foreach(var f in Directory.GetFiles(output).OrderBy(x=>x))if(!f.EndsWith("validation-report.txt",StringComparison.OrdinalIgnoreCase))report.AppendLine(Path.GetFileName(f)+": "+Hash(File.ReadAllBytes(f)));
        File.WriteAllText(Path.Combine(output,"validation-report.txt"),report.ToString());
    }
}
