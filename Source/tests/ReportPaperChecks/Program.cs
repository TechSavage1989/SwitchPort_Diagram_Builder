using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using SwitchportGenerator;

static class ReportPaperChecks
{
    static int checks;
    static void Check(bool condition,string message)
    {
        checks++;
        if(!condition)throw new InvalidOperationException(message);
    }

    static void AssertPdf(string name,string orientation,double widthMm,double heightMm)
    {
        var paper=ReportPaper.Parse(name,orientation);paper.Validate();
        Check(Math.Abs(paper.SizeMm.Width-widthMm)<.01&&Math.Abs(paper.SizeMm.Height-heightMm)<.01,"Incorrect paper dimensions.");
        string file=Path.Combine(Path.GetTempPath(),"switchport-media-"+Guid.NewGuid().ToString("N")+".pdf");
        try
        {
            Pdf.Save(new(){new Scene{Width=805.89,Height=559.28}},file,paper);
            string content=Encoding.Latin1.GetString(File.ReadAllBytes(file));
            var box=Regex.Match(content,@"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
            Check(box.Success,"A PDF MediaBox was not written.");
            double width=double.Parse(box.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
            double height=double.Parse(box.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
            Check(Math.Abs(width-paper.WidthPt)<=.55&&Math.Abs(height-paper.HeightPt)<=.55,
                "Actual PDF MediaBox does not match "+paper.Description+": "+width+" x "+height);
        }
        finally{File.Delete(file);}
    }

    static void AssertPng(string name,string orientation)
    {
        var paper=ReportPaper.Parse(name,orientation);
        string file=Path.Combine(Path.GetTempPath(),"switchport-png-"+Guid.NewGuid().ToString("N")+".png");
        try
        {
            Png.Save(new Scene{Width=805.89,Height=559.28},file,paper.PixelWidth,paper.PixelHeight,true);
            byte[] data=File.ReadAllBytes(file);
            Check(data.Length>90&&BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16,4))==paper.PixelWidth&&
                BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20,4))==paper.PixelHeight,
                "PNG dimensions do not match "+paper.Description);
            int physical=-1;
            for(int offset=8;offset+12<=data.Length;)
            {
                int length=BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset,4));
                Check(length>=0&&length<=data.Length-offset-12,"Invalid PNG chunk length.");
                if(data.AsSpan(offset+4,4).SequenceEqual("pHYs"u8))
                {Check(length==9,"Invalid physical-resolution PNG chunk.");physical=offset+8;break;}
                offset+=length+12;
            }
            Check(physical>=0,"Missing physical-resolution PNG chunk.");
            Check(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(physical,4))==11811&&
                BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(physical+4,4))==11811&&data[physical+8]==1,
                "PNG must declare 300 DPI in both axes using pixels per metre.");
        }
        finally{File.Delete(file);}
    }

    static void ExternalLabels()
    {
        string config="; JL256A Configuration Editor\nhostname LABEL-TEST\nvlan 10\n untagged 1-52\n exit\n";
        var inv=Parser.Parse(config);
        var output=QuickSheet.Build(inv,Charts.Layout(inv),"PAPER-LABEL");
        Check(Charts.ValidateScene(output.Scene).Count==0,"External port numbers must not overlap the scene geometry.");
        var group=inv.Groups.Single();
        var rows=output.Sockets.Select(s=>s.Tile.Y).Distinct().Order().ToArray();
        Check(rows.Length==2,"Fixture must have two physical socket rows.");
        foreach(var socket in output.Sockets)
        {
            var label=output.Scene.Labels.Single(l=>l.Owner==socket.Port);
            var text=ReportRenderer.Measure(label);
            int row=Array.IndexOf(rows,socket.Tile.Y);
            Check(Math.Abs(label.X+text.Width/2-socket.Tile.X-socket.Tile.W/2)<.03,
                "Port number not centered over its socket: "+socket.Port);
            if(row%2==0)Check(label.Y+text.Height<socket.Tile.Y-.25,
                "Top-row number must be above the port: "+socket.Port);
            else Check(label.Y>socket.Tile.Y+socket.Tile.H+.25,
                "Bottom-row number must be below the port: "+socket.Port);
            Check(label.InkBox&&label.Bold&&label.Size>=QuickSheet.MinimumPortFont-.01,
                "External numbers must remain bold and readable.");
        }
        Check(output.Sockets.Any(s=>s.Optical)&&output.Sockets.Any(s=>!s.Optical),
            "Mixed optical and RJ45 bank was not tested.");
        // Four socket rows require room between the two row pairs.
        var multi=Parser.Parse("! AOS-CX JL479A\nhostname FOUR-ROW\n");
        var multiOut=QuickSheet.Build(multi,Charts.Layout(multi),"MULTI-ROW");
        Check(multiOut.Sockets.Count==multi.Ports.Count,"Multirow faceplate lost physical ports.");
        Check(Charts.ValidateScene(multiOut.Scene).Count==0,
            "Multirow number bands collide with physical socket geometry.");
    }

    static void Workspace()
    {
        var workspace=new ConfigurationWorkspace();
        workspace.AddText("; JL256A Configuration Editor\nhostname PAPER-WORKSPACE\n","paper.cfg");
        workspace.Paper=ReportPaper.Parse("letter","portrait");
        var restored=ConfigurationWorkspace.Deserialize(workspace.Serialize());
        Check(restored.Paper.Preset=="letter"&&restored.Paper.Orientation=="portrait",
            "Paper preferences did not survive a saved workspace round trip.");
        var legacy=workspace.Serialize().Replace("\"Paper\": {\n    \"Preset\": \"letter\",\n    \"Orientation\": \"portrait\"\n  },","",StringComparison.Ordinal);
        // Old workspaces can omit Paper entirely; deserialize a document that
        // omits the property rather than changing the workspace version.
        using var parsed=System.Text.Json.JsonDocument.Parse(workspace.Serialize());
        var original=System.Text.Json.Nodes.JsonNode.Parse(workspace.Serialize())!.AsObject();
        original.Remove("Paper");
        var old=ConfigurationWorkspace.Deserialize(original.ToJsonString());
        Check(old.Paper.IsA4Default&&old.Items.Count==1,"Legacy version-1 workspace changed defaults or lost configurations.");
        bool invalid=false;try{ReportPaper.Parse("a1","landscape");}catch(InvalidOperationException){invalid=true;}
        Check(invalid,"Unsupported paper size must be rejected.");
        invalid=false;try{ReportPaper.Parse("letter","tilted");}catch(InvalidOperationException){invalid=true;}
        Check(invalid,"Unsupported orientation must be rejected.");
    }

    static void ExportPaper()
    {
        string root=Path.Combine(Path.GetTempPath(),"switchport-paper-export-"+Guid.NewGuid().ToString("N"));
        try
        {
            var workspace=new ConfigurationWorkspace{Paper=ReportPaper.Parse("letter","portrait")};
            workspace.AddText("; JL256A Configuration Editor\nhostname PAPER-ACCESS\n","access.txt");
            workspace.AddText("! AOS-CX JL679A\nhostname PAPER-FIBER\n","fiber.txt");
            var result=WorkspaceExport.Export(workspace,root);
            Check(result.SuccessCount==2&&result.FailureCount==0&&result.CombinedError.Length==0,
                "Selected-paper individual and shared exports must succeed.");
            Check(Path.GetFileName(result.CombinedDiagramsPath)=="workspace-letter-portrait-diagrams.pdf"&&
                Path.GetFileName(result.CombinedBookletPath)=="workspace-letter-portrait-booklets.pdf",
                "Shared reports must use the selected paper filename stem.");
            foreach(var item in result.Items)
            {
                Check(Directory.GetFiles(item.OutputDirectory).Length==8,"Individual reports must preserve the eight-file baseline.");
                foreach(string name in new[]{"letter-portrait-one-page.pdf","letter-portrait-one-page.png","letter-portrait-booklet.pdf"})
                    Check(File.Exists(Path.Combine(item.OutputDirectory,name)),"Missing selected-paper output: "+name);
            }
            foreach(string file in Directory.GetFiles(root,"*.pdf",SearchOption.AllDirectories))
            {
                var boxes=Regex.Matches(Encoding.Latin1.GetString(File.ReadAllBytes(file)),@"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
                Check(boxes.Count>0,"Exported PDF has no physical media box: "+file);
                foreach(Match box in boxes)
                    Check(Math.Abs(double.Parse(box.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)-612)<.55&&
                        Math.Abs(double.Parse(box.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture)-792)<.55,
                        "Every individual/shared PDF page must use US Letter portrait.");
            }
            foreach(string file in result.CombinedImages.Concat(result.Items.Select(item=>Path.Combine(item.OutputDirectory,"letter-portrait-one-page.png"))))
            {
                byte[] png=File.ReadAllBytes(file);
                Check(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4))==2550&&BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4))==3300,
                    "Individual/shared paper previews must use US Letter portrait at 300 DPI.");
            }
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    public static int Main()
    {
        Workspace();
        ExternalLabels();
        foreach(var (name,width,height) in new[]{("letter",215.9,279.4),("legal",215.9,355.6),("tabloid",279.4,431.8),
            ("a5",148.0,210.0),("a4",210.0,297.0),("a3",297.0,420.0),("a2",420.0,594.0)})
        {AssertPdf(name,"portrait",width,height);AssertPdf(name,"landscape",height,width);}
        AssertPng("a4","landscape");AssertPng("letter","landscape");AssertPng("letter","portrait");
        ExportPaper();
        Console.WriteLine("PASS ReportPaperChecks ("+checks+" checks).");
        return 0;
    }
}
