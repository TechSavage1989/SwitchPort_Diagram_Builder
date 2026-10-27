using System.Text.Json;
using SwitchportGenerator;

static class WorkspacePaletteChecks
{
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static void Reject(Action action,string message)
    {
        bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,message);
    }
    static string Source(string host,string name="Data",int native=10)=>
        "; JL256A Configuration Editor\nhostname "+host+"\ntrunk 1-2 trk1 lacp\nvlan 10\n name \""+name+"\"\n "+(native==10?"untagged":"tagged")+" trk1\n exit\nvlan 20\n name Voice\n "+(native==20?"untagged":"tagged")+" trk1\n exit\nvlan 99\n exit\n";
    static int Main()
    {
        try{Run();Console.WriteLine("PASS "+checks+" shared workspace VLAN/LAG palette, identity, persistence and contrast assertions.");return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    static void Run()
    {
        var workspace=new ConfigurationWorkspace();
        var first=workspace.AddText(Source("FIRST"),"first.cfg");
        var second=workspace.AddText(Source("SECOND","Data",20),"second.cfg");
        var conflict=workspace.AddText(Source("CONFLICT","Guest"),"conflict.cfg");
        Check(workspace.Items.All(item=>item.Inventory.Blockers.Count==0),"Shared-palette fixtures must remain valid configurations.");
        var plan=WorkspacePalette.Create(workspace);
        var a=plan.ForItem(first.Id);var b=plan.ForItem(second.Id);var c=plan.ForItem(conflict.Id);
        Check(a.Vlans[10]==b.Vlans[10]&&a.Vlans[20]==b.Vlans[20],"Matching VLAN ID and name must share colors across independently parsed configurations.");
        Check(a.Vlans[10]!=c.Vlans[10],"Same VLAN ID with conflicting names must have different colors.");
        Check(a.Vlans[99]!=b.Vlans[99]&&b.Vlans[99]!=c.Vlans[99],"Unnamed VLANs remain item-scoped rather than silently merged.");
        Check(plan.Warnings.Any(warning=>warning.Contains("VLAN 10 has conflicting names"))&&plan.Warnings.Any(warning=>warning.Contains("VLAN 99 is unnamed")),"Ambiguous VLAN identity requires explicit warnings.");
        Check(plan.Warnings.Any(warning=>warning.Contains("does not establish a physical link")),"Shared LAG colors must not imply cross-device physical connectivity.");
        string firstLag=first.Inventory.Ports.First(port=>port.Aggregate.Length>0).Aggregate;
        string secondLag=second.Inventory.Ports.First(port=>port.Aggregate.Length>0).Aggregate;
        Check(a.Aggregates[firstLag]==b.Aggregates[secondLag],"Matching normalized LAG labels must share outline colors.");
        foreach(var item in workspace.Items)
        {
            plan.Apply(item.Id,item.Inventory);
            var fill=VlanPalette.Assign(item.Inventory);
            var outlines=AggregatePalette.Assign(item.Inventory,port=>VlanPalette.NativeFill(port,fill),fill.Values);
            Check(fill.OrderBy(pair=>pair.Key).SequenceEqual(plan.ForItem(item.Id).Vlans.OrderBy(pair=>pair.Key)),"Renderer must consume the complete shared VLAN context unchanged.");
            foreach(var port in item.Inventory.Ports.Where(port=>port.Aggregate.Length>0))
                Check(AggregatePalette.Difference(outlines[port.Aggregate],VlanPalette.NativeFill(port,fill))>=AggregatePalette.MinimumMemberDistance,
                    "Shared LAG outline must contrast with every member fill across all configurations.");
            Check(item.Inventory.Assumptions.Contains(plan.Warnings[0]),"Palette identity warnings must propagate into report metadata.");
            var roundtrip=JsonSerializer.Deserialize<Inventory>(JsonSerializer.Serialize(item.Inventory))!;
            Check(VlanPalette.Assign(roundtrip).SequenceEqual(fill),"Exported inventory must retain shared VLAN colors for combined reconstruction.");
            Check(AggregatePalette.Assign(roundtrip,port=>VlanPalette.NativeFill(port,fill),fill.Values).OrderBy(pair=>pair.Key).SequenceEqual(outlines.OrderBy(pair=>pair.Key)),
                "Exported inventory must retain shared LAG colors for combined reconstruction.");
        }
        string[] original=workspace.Items.OrderBy(item=>item.Id).Select(item=>plan.ForItem(item.Id).Serialize()).ToArray();
        workspace.Move(conflict.Id,0);workspace.Select(second.Id);
        var reordered=WorkspacePalette.Create(workspace);
        Check(workspace.Items.OrderBy(item=>item.Id).Select(item=>reordered.ForItem(item.Id).Serialize()).SequenceEqual(original),"Reorder and selection must not change shared color choices.");
        var reopened=ConfigurationWorkspace.Deserialize(workspace.Serialize());
        var loaded=WorkspacePalette.Create(reopened);
        Check(reopened.Items.OrderBy(item=>item.Id).Select(item=>loaded.ForItem(item.Id).Serialize()).SequenceEqual(original),"Workspace roundtrip must reproduce named and unnamed color identities.");
        Check(WorkspacePalette.AggregateKey("  TrK1 ")==WorkspacePalette.AggregateKey("trk1"),"LAG matching trims and normalizes case consistently.");
        var named=new Inventory{Vlans=new(){new Vlan{Id=50,Name="  Caf\u00e9 "}}};
        var decomposed=new Inventory{Vlans=new(){new Vlan{Id=50,Name="Cafe\u0301"}}};
        Check(WorkspacePalette.VlanKey(first.Id,named,50)==WorkspacePalette.VlanKey(second.Id,decomposed,50),"VLAN names use canonical Unicode normalization and surrounding trim.");
        decomposed.Vlans[0].Name="CAFÉ";
        Check(WorkspacePalette.VlanKey(first.Id,named,50)!=WorkspacePalette.VlanKey(second.Id,decomposed,50),"Different case in VLAN names remains an explicit distinct identity.");

        var single=new ConfigurationWorkspace();var only=single.AddText(Source("SINGLE"),"single.cfg");
        var baseline=VlanPalette.Assign(only.Inventory);
        var baselineLags=AggregatePalette.Assign(only.Inventory,port=>VlanPalette.NativeFill(port,baseline),baseline.Values);
        var singlePlan=WorkspacePalette.Create(single).ForItem(only.Id);
        Check(singlePlan.Vlans.SequenceEqual(baseline)&&singlePlan.Aggregates.OrderBy(pair=>pair.Key).SequenceEqual(baselineLags.OrderBy(pair=>pair.Key)),
            "One-item workspace without overrides preserves existing standalone palette choices.");
        string key=plan.VlanIdentities[first.Id][10];
        var overrides=new WorkspacePaletteOverrides();overrides.Vlans[key]="#abcdef";
        var custom=WorkspacePalette.Create(workspace,overrides);
        Check(custom.ForItem(first.Id).Vlans[10]=="#abcdef"&&custom.ForItem(second.Id).Vlans[10]=="#abcdef", "A saved semantic VLAN override applies to every matching configuration.");
        Check(custom.ForItem(conflict.Id).Vlans[10]!="#abcdef", "Conflicting named VLAN identity does not inherit an override.");
        var savedOverrides=JsonSerializer.Deserialize<WorkspacePaletteOverrides>(JsonSerializer.Serialize(overrides))!;
        Check(WorkspacePalette.Create(reopened,savedOverrides).ForItem(first.Id).Vlans[10]=="#abcdef", "Saved overrides remain portable across workspace reload.");
        workspace.PaletteOverrides=overrides;
        var savedWorkspace=ConfigurationWorkspace.Deserialize(workspace.Serialize());
        Check(WorkspacePalette.Create(savedWorkspace).ForItem(first.Id).Vlans[10]=="#abcdef", "Workspace format persistence and default palette creation must honor stored overrides.");
        overrides.Aggregates[WorkspacePalette.AggregateKey(firstLag)]=custom.ForItem(first.Id).Aggregates[firstLag];
        Check(WorkspacePalette.Create(workspace,overrides).ForItem(second.Id).Aggregates[secondLag]==overrides.Aggregates[WorkspacePalette.AggregateKey(firstLag)],
            "A contrast-safe saved LAG override is honored across all member contexts.");
        var duplicateColors=new WorkspacePaletteOverrides{Vlans=new(){[key]="#abcdef",[plan.VlanIdentities[conflict.Id][10]]="#abcdef"}};
        Reject(()=>WorkspacePalette.Create(workspace,duplicateColors),"Distinct identities cannot receive duplicate overridden VLAN colors.");
        Reject(()=>WorkspacePalette.Create(workspace,new(){Vlans=new(){["missing-identity"]="#abcdef"}}),"Missing saved identity keys must fail explicitly.");
        Reject(()=>WorkspacePalette.Create(workspace,new(){Vlans=new(){[key]="red"}}),"Malformed color values must fail explicitly.");
        foreach(string reserved in new[]{"#ffffff","#e6e6e6","#000000"})
            Reject(()=>WorkspacePalette.Create(workspace,new(){Vlans=new(){[key]=reserved}}),"VLAN overrides cannot impersonate reserved None/Unknown/Stack colors.");
        Reject(()=>WorkspacePalette.Create(workspace,new(){Aggregates=new(){[WorkspacePalette.AggregateKey(firstLag)]=a.Vlans[10]}}),"Unsafe LAG overrides cannot bypass member-fill contrast guards.");
        var decoded=RenderPalette.Deserialize(a.Serialize());
        var fresh=DeviceModels.Parse(first.Text,first.Devices);decoded.Apply(fresh);
        Check(VlanPalette.Assign(fresh).SequenceEqual(a.Vlans),"Per-item render context survives the child-process JSON boundary.");
        decoded.Vlans.Remove(10);
        Reject(()=>decoded.Apply(fresh),"Incomplete render contexts cannot silently recompute different colors.");
        Reject(()=>RenderPalette.Deserialize("{bad"),"Malformed render context JSON must fail clearly.");
        Check(VlanPalette.Assign(fresh).SequenceEqual(a.Vlans),"Failed render-context application preserves the previous valid context.");
        RemovalLifecycle();
        BlockedIsolation();
    }

    static void RemovalLifecycle()
    {
        var workspace=new ConfigurationWorkspace();
        var first=workspace.AddText(Source("REMOVE-FIRST"),"first.cfg");
        var second=workspace.AddText(Source("KEEP-SHARED","Data",20),"second.cfg");
        var exclusive=workspace.AddText(Source("REMOVE-EXCLUSIVE","Exclusive").Replace("trk1","trk2"),"exclusive.cfg");
        var plan=WorkspacePalette.Create(workspace);
        foreach(var item in workspace.Items)
        {
            foreach(var pair in plan.VlanIdentities[item.Id])workspace.PaletteOverrides.Vlans[pair.Value]=plan.ForItem(item.Id).Vlans[pair.Key];
            foreach(var pair in plan.ForItem(item.Id).Aggregates)workspace.PaletteOverrides.Aggregates[WorkspacePalette.AggregateKey(pair.Key)]=pair.Value;
        }
        string unnamed=plan.VlanIdentities[first.Id][99],shared=plan.VlanIdentities[first.Id][10],privateName=plan.VlanIdentities[exclusive.Id][10];
        string sharedColor=workspace.PaletteOverrides.Vlans[shared],sharedOutline=workspace.PaletteOverrides.Aggregates["TRK1"];
        workspace.Remove(first.Id);
        Check(!workspace.PaletteOverrides.Vlans.ContainsKey(unnamed),"Removing an item prunes its sole-owner unnamed VLAN override.");
        Check(workspace.PaletteOverrides.Vlans[shared]==sharedColor&&workspace.PaletteOverrides.Aggregates["TRK1"]==sharedOutline,
            "Removing one owner retains shared named VLAN and LAG overrides.");
        var reopened=ConfigurationWorkspace.Deserialize(workspace.Serialize());
        Check(WorkspacePalette.Create(reopened).ForItem(second.Id).Vlans[10]==sharedColor,
            "Workspace save/load remains valid after pruning removed-item override identities.");
        workspace.Remove(exclusive.Id);
        Check(!workspace.PaletteOverrides.Vlans.ContainsKey(privateName)&&!workspace.PaletteOverrides.Aggregates.ContainsKey("TRK2"),
            "Sole-owner named VLAN and LAG overrides are pruned when their final owner is removed.");
        var remaining=ConfigurationWorkspace.Deserialize(workspace.Serialize());
        Check(WorkspacePalette.Create(remaining).ForItem(second.Id).Aggregates.Values.Single()==sharedOutline,
            "Surviving shared LAG color remains unchanged after removal and reload.");
        workspace.Remove(second.Id);
        Check(workspace.PaletteOverrides.Vlans.Count==0&&workspace.PaletteOverrides.Aggregates.Count==0
            &&ConfigurationWorkspace.Deserialize(workspace.Serialize()).Items.Count==0,
            "Removing the final owner clears orphan overrides and leaves a valid empty saved workspace.");
    }

    static void BlockedIsolation()
    {
        var workspace=new ConfigurationWorkspace();
        var valid=workspace.AddText(Source("VALID"),"valid.cfg");
        string baseline=WorkspacePalette.Create(workspace).ForItem(valid.Id).Serialize();
        var blocked=workspace.AddText("unsupported hardware configuration","blocked.cfg");
        // A huge parsed VLAN list on an already-blocked configuration must not
        // exhaust the finite perceptual palette and prevent the valid export.
        blocked.Inventory.Vlans=Enumerable.Range(1,4094).Select(id=>new Vlan{Id=id,Name="Blocked VLAN "+id}).ToList();
        blocked.Inventory.Ports.Add(new Port{Id="blocked",Aggregate="blocked-lag",UntaggedVlan=1});
        string blockedKey=WorkspacePalette.VlanKey(blocked.Id,blocked.Inventory,1);
        workspace.PaletteOverrides.Vlans[blockedKey]="#abcdef";
        workspace.PaletteOverrides.Aggregates[WorkspacePalette.AggregateKey("blocked-lag")]="#123456";
        var plan=WorkspacePalette.Create(workspace);
        Check(plan.Items.Count==1&&plan.Items.ContainsKey(valid.Id)&&!plan.Items.ContainsKey(blocked.Id),
            "A blocked 4094-VLAN inventory must not participate in shared palette optimization.");
        var before=RenderPalette.Deserialize(baseline);var after=plan.ForItem(valid.Id);
        Check(before.Vlans.SequenceEqual(after.Vlans)&&before.Aggregates.SequenceEqual(after.Aggregates),
            "Blocked inputs cannot alter the valid configuration's VLAN/LAG colors.");
        Check(workspace.PaletteOverrides.Vlans.ContainsKey(blockedKey)&&workspace.PaletteOverrides.Aggregates.ContainsKey("BLOCKED-LAG"),
            "Overrides owned only by blocked configurations remain saved for later correction.");
        Check(plan.Warnings.Any(warning=>warning.Contains("excluded from the shared palette")),"Blocked-item palette exclusion remains explicit.");
        workspace.PaletteOverrides.Vlans["not-an-identity"]="#abcdef";
        Reject(()=>WorkspacePalette.Create(workspace),"Even with blocked items present, truly unknown override identity keys must fail.");
    }
}
