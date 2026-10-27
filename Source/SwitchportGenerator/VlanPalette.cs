using System.Collections.Concurrent;
namespace SwitchportGenerator;

public static class VlanPalette
{
    // Retained as the preferred Lab separation floor, not a promise that an
    // arbitrarily large number of pastel categories can all remain distinct.
    public const double MinimumDistance=18;
    record Candidate(string Hex,(double L,double A,double B) Lab);
    static readonly List<Candidate> Candidates=MakeCandidates();
    static readonly int Count=Candidates.Count;
    static readonly (double L,double A,double B)[] References={AggregatePalette.Lab("#ffffff"),AggregatePalette.Lab("#e6e6e6"),AggregatePalette.Lab("#000000"),AggregatePalette.Lab("#9faab5")};
    static readonly ConcurrentDictionary<(int Count,bool DefaultVlan),Lazy<int[]>> Palettes=new();
    // CIEDE2000 is more expensive than Euclidean Lab. Cache symmetric candidate
    // comparisons and whole palette searches, which depend on count, not IDs.
    static readonly float[] Distances=new float[(Count+References.Length)*(Count+References.Length)];
    static readonly object SearchLock=new();
    public static string NativeFill(Port p,IReadOnlyDictionary<int,string> palette)=>p.StackLink?"#000000":p.UntaggedState=="None"?"#ffffff":p.UntaggedVlan.HasValue?palette[p.UntaggedVlan.Value]:"#e6e6e6";
    static List<Candidate> MakeCandidates()
    {
        var values=new HashSet<string>();
        foreach(double lightness in new[]{.52,.58,.64,.70,.76,.82,.87})foreach(double saturation in new[]{.32,.45,.58,.72,.86})for(int hue=0;hue<360;hue+=6)
        {
            double h=hue/60.0,c=(1-Math.Abs(2*lightness-1))*saturation,x=c*(1-Math.Abs(h%2-1)),m=lightness-c/2;
            (double r,double g,double b)=h<1?(c,x,0.0):h<2?(x,c,0.0):h<3?(0.0,c,x):h<4?(0.0,x,c):h<5?(x,0.0,c):(c,0.0,x);
            values.Add($"#{(int)Math.Round((r+m)*255):x2}{(int)Math.Round((g+m)*255):x2}{(int)Math.Round((b+m)*255):x2}");
        }
        return values.Select(hex=>new Candidate(hex,AggregatePalette.Lab(hex))).Where(c=>
        {
            var rgb=SkiaSharp.SKColor.Parse(c.Hex);double chroma=Math.Sqrt(c.Lab.A*c.Lab.A+c.Lab.B*c.Lab.B);
            return c.Lab.L>=60&&c.Lab.L<=91&&chroma>=14&&chroma<=70&&Math.Min(rgb.Red,Math.Min(rgb.Green,rgb.Blue))>=95;
        }).OrderBy(c=>c.Hex,StringComparer.Ordinal).ToList();
    }
    static (double L,double A,double B) Lab(int i)=>i<Count?Candidates[i].Lab:References[i-Count];
    static double Distance(int first,int second)
    {
        if(first==second)return 0;
        if(first>second)(first,second)=(second,first);
        int index=first*(Count+References.Length)+second;float value=Distances[index];
        if(value==0)Distances[index]=value=(float)PerceptualColor.Difference(Lab(first),Lab(second));
        return value;
    }
    static double LabSquared(int first,int second)
    {var a=Lab(first);var b=Lab(second);double l=a.L-b.L,x=a.A-b.A,y=a.B-b.B;return l*l+x*x+y*y;}
    static List<int> Reserved(bool defaultVlan)=>Enumerable.Range(Count,defaultVlan?4:3).ToList();
    static (double Minimum,double Secondary) Score(List<int> selected,List<int> reserved)
    {
        var distances=new List<double>();
        for(int i=0;i<selected.Count;i++)
        {
            foreach(int reference in reserved.Where(r=>r==Count+3))distances.Add(Distance(selected[i],reference));
            for(int j=0;j<i;j++)distances.Add(Distance(selected[i],selected[j]));
        }
        if(distances.Count==0)return (100,selected.Select(i=>reserved.Min(r=>Distance(i,r))).DefaultIfEmpty(100).Min());
        distances.Sort();return (distances[0],distances.Take(Math.Max(1,selected.Count)).Average());
    }
    static List<int>? Greedy(int size,List<int> reserved,int seed,double labFloor)
    {
        var background=Enumerable.Range(0,Count).Select(i=>reserved.Min(r=>Distance(i,r))).ToArray();
        var nearest=Enumerable.Range(0,Count).Select(i=>reserved.Contains(Count+3)?Distance(i,Count+3):100).ToArray();
        var legacy=Enumerable.Range(0,Count).Select(i=>reserved.Min(r=>LabSquared(i,r))).ToArray();
        var selected=new List<int>();double threshold=labFloor*labFloor;
        while(selected.Count<size)
        {
            int chosen=-1;double score=-1;
            for(int i=0;i<Count;i++)
            {
                if(nearest[i]<0||legacy[i]+1e-6<threshold||background[i]<10)continue;
                double value=selected.Count==0&&!reserved.Contains(Count+3)?background[i]:nearest[i];
                // Different deterministic first hues avoid committing every
                // count to one early greedy choice. Later picks maximize the
                // weakest perceptual separation without hue preferences.
                if(selected.Count==0&&seed>0)
                {
                    var lab=Candidates[i].Lab;double h=Math.Atan2(lab.B,lab.A)*180/Math.PI;if(h<0)h+=360;
                    double gap=Math.Abs(h-(seed-1)*60);gap=Math.Min(gap,360-gap);
                    value*=.65+.35*Math.Cos(gap*Math.PI/180);
                }
                if(value>score){chosen=i;score=value;}
            }
            if(chosen<0)return null;
            selected.Add(chosen);nearest[chosen]=-1;
            for(int i=0;i<Count;i++)if(nearest[i]>=0)
            {nearest[i]=Math.Min(nearest[i],Distance(i,chosen));legacy[i]=Math.Min(legacy[i],LabSquared(i,chosen));}
        }
        return selected;
    }
    static void Refine(List<int> selected,List<int> reserved,double labFloor)
    {
        double threshold=labFloor*labFloor;
        for(int pass=0;pass<3;pass++)
        {
            bool changed=false;
            var weakest=Enumerable.Range(0,selected.Count).OrderBy(i=>selected.Where((_,n)=>n!=i).Concat(reserved.Where(r=>r==Count+3)).Select(j=>Distance(selected[i],j)).DefaultIfEmpty(100).Min()).Take(12).ToArray();
            foreach(int slot in weakest)
            {
                var others=selected.Where((_,n)=>n!=slot).Concat(reserved.Where(r=>r==Count+3)).ToArray();
                if(others.Length==0)continue;
                int old=selected[slot],best=old;double separation=others.Min(j=>Distance(old,j));
                for(int candidate=0;candidate<Count;candidate++)
                {
                    double nearest=100;bool fits=reserved.All(r=>LabSquared(candidate,r)+1e-6>=threshold&&Distance(candidate,r)>=10);
                    if(!fits)continue;
                    foreach(int other in others)
                    {
                        if(LabSquared(candidate,other)+1e-6<threshold){fits=false;break;}
                        nearest=Math.Min(nearest,Distance(candidate,other));if(nearest<=separation+1e-6){fits=false;break;}
                    }
                    if(fits){best=candidate;separation=nearest;}
                }
                if(best!=old){selected[slot]=best;changed=true;}
            }
            if(!changed)break;
        }
    }
    static int[] Search(int size,bool defaultVlan)
    {
        if(size==0)return Array.Empty<int>();
        if(size>Count)throw new OnePageCapacityException("VLAN count exceeds the available distinct soft-color candidates");
        lock(SearchLock)
        {
            var reserved=Reserved(defaultVlan);List<int>? best=null;var bestScore=(Minimum:-1.0,Secondary:-1.0);
            // A crowded key should use the best available separated palette,
            // rather than fail only because a fixed color-distance floor is
            // no longer possible. The preferred 18-Lab guard is tried first.
            foreach(double floor in new[]{MinimumDistance,16.0,14,12,10,8,6,4,2,0})
            {
                for(int seed=0;seed<7;seed++)
                {
                    var selected=Greedy(size,reserved,seed,floor);if(selected==null)continue;
                    Refine(selected,reserved,floor);var score=Score(selected,reserved);
                    if(score.Minimum>bestScore.Minimum+1e-5||Math.Abs(score.Minimum-bestScore.Minimum)<=1e-5&&score.Secondary>bestScore.Secondary)
                    {best=selected;bestScore=score;}
                }
                if(best!=null&&size+(defaultVlan?1:0)<=28)break;
            }
            return best?.ToArray()??throw new OnePageCapacityException("VLAN count exceeds available distinct soft colors");
        }
    }
    public static Dictionary<int,string> Assign(Inventory inv)
    {
        var ids=inv.Vlans.Select(v=>v.Id).Concat(inv.Ports.SelectMany(p=>p.TaggedVlans.Concat(p.UntaggedVlan.HasValue?new[]{p.UntaggedVlan.Value}:Array.Empty<int>()).Concat(p.VoiceVlan.HasValue?new[]{p.VoiceVlan.Value}:Array.Empty<int>()))).Distinct().Order().ToList();
        var pinned=inv.VlanColorOverrides??new Dictionary<int,string>();
        if(pinned.Keys.Any(id=>!ids.Contains(id)))throw new InvalidOperationException("VLAN palette contains an identity absent from this configuration.");
        foreach(string color in pinned.Values)RenderPalette.ValidateVlanColor(color);
        if(pinned.Values.Select(color=>color.ToLowerInvariant()).Distinct().Count()!=pinned.Count)
            throw new InvalidOperationException("Distinct VLAN identities cannot share an overridden color.");
        if(pinned.Count==ids.Count)return ids.ToDictionary(id=>id,id=>pinned[id].ToLowerInvariant());
        var result=new Dictionary<int,string>();bool defaultVlan=ids.Contains(1);if(defaultVlan)result.Add(1,"#9faab5");
        int size=ids.Count-result.Count;
        var available=Palettes.GetOrAdd((size,defaultVlan),key=>new(()=>Search(key.Count,key.DefaultVlan))).Value.ToList();
        // Familiar approximate hues are only a mapping preference after the
        // complete separated palette has been chosen. Context swaps below
        // may override that preference to separate VLANs sharing a port.
        int[] familiar={10,110,120,140,150,160,254,300,500,600};
        foreach(int id in ids.Where(id=>id!=1).OrderBy(id=>Array.IndexOf(familiar,id)<0?100:Array.IndexOf(familiar,id)).ThenBy(id=>id))
        {
            var original=SkiaSharp.SKColor.Parse(Charts.VlanColor(id));
            string softened=$"#{(int)Math.Round(original.Red*.65+255*.35):x2}{(int)Math.Round(original.Green*.65+255*.35):x2}{(int)Math.Round(original.Blue*.65+255*.35):x2}";
            var target=AggregatePalette.Lab(softened);
            int chosen=available.OrderBy(c=>PerceptualColor.Difference(Candidates[c].Lab,target)).ThenBy(c=>Candidates[c].Hex,StringComparer.Ordinal).First();
            result.Add(id,Candidates[chosen].Hex);available.Remove(chosen);
        }
        ImproveMapping(inv,ids,result);
        if(pinned.Count>0)
        {
            foreach(var pair in pinned)result[pair.Key]=pair.Value.ToLowerInvariant();
            var used=pinned.Values.Select(color=>color.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Keep pinned choices exact. Reassign remaining identities against all pinned
            // colors with the same perceptual candidates instead of permitting collisions.
            foreach(int id in ids.Where(id=>!pinned.ContainsKey(id)))
            {
                string chosen=used.Count==0?result[id]:Candidates.Where(candidate=>!used.Contains(candidate.Hex)
                    &&References.Take(3).All(reference=>PerceptualColor.Difference(candidate.Lab,reference)>=10))
                    .OrderByDescending(candidate=>used.Min(color=>PerceptualColor.Difference(candidate.Hex,color)))
                    .ThenBy(candidate=>candidate.Hex,StringComparer.Ordinal).First().Hex;
                result[id]=chosen;used.Add(chosen);
            }
        }
        return result;
    }
    static void ImproveMapping(Inventory inv,List<int> ids,Dictionary<int,string> colors)
    {
        if(ids.Count<3||ids.Count>96)return;
        int n=ids.Count;var index=ids.Select((id,i)=>(id,i)).ToDictionary(p=>p.id,p=>p.i);var weights=new double[n,n];
        void Edge(int first,int second,double weight)
        {if(first==second||!index.TryGetValue(first,out int a)||!index.TryGetValue(second,out int b))return;weights[a,b]+=weight;weights[b,a]+=weight;}
        for(int i=1;i<n;i++)Edge(ids[i-1],ids[i],.25);
        foreach(var p in inv.Ports)
        {
            var members=p.TaggedVlans.Concat(p.VoiceVlan.HasValue?new[]{p.VoiceVlan.Value}:Array.Empty<int>()).Concat(p.UntaggedVlan.HasValue?new[]{p.UntaggedVlan.Value}:Array.Empty<int>()).Distinct().ToArray();
            for(int i=0;i<members.Length;i++)for(int j=0;j<i;j++)Edge(members[i],members[j],2);
        }
        for(int i=1;i<inv.Ports.Count;i++)
        {var a=inv.Ports[i-1];var b=inv.Ports[i];if(a.Group==b.Group&&a.UntaggedVlan.HasValue&&b.UntaggedVlan.HasValue)Edge(a.UntaggedVlan.Value,b.UntaggedVlan.Value,1);}
        var hex=ids.Select(id=>colors[id]).ToArray();var labs=hex.Select(AggregatePalette.Lab).ToArray();var penalty=new double[n,n];
        for(int i=0;i<n;i++)for(int j=0;j<i;j++){double d=PerceptualColor.Difference(labs[i],labs[j]);double p=1/Math.Pow(Math.Max(1,d),4);penalty[i,j]=penalty[j,i]=p;}
        var assigned=Enumerable.Range(0,n).ToArray();
        for(int pass=0;pass<4;pass++)
        {
            bool changed=false;
            for(int a=0;a<n;a++)for(int b=a+1;b<n;b++)
            {
                if(ids[a]==1||ids[b]==1)continue;
                double delta=0;
                for(int k=0;k<n;k++)if(k!=a&&k!=b)delta+=weights[a,k]*(penalty[assigned[b],assigned[k]]-penalty[assigned[a],assigned[k]])+weights[b,k]*(penalty[assigned[a],assigned[k]]-penalty[assigned[b],assigned[k]]);
                if(delta< -1e-12){(assigned[a],assigned[b])=(assigned[b],assigned[a]);changed=true;}
            }
            if(!changed)break;
        }
        for(int i=0;i<n;i++)colors[ids[i]]=hex[assigned[i]];
    }
}
