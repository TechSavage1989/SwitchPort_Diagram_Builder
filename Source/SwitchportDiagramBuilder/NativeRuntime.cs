using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;
namespace SwitchportDiagramBuilder;
internal static class NativeRuntime
{
    static readonly List<IntPtr> Handles=new();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool SetDllDirectory(string path);
    public static void Prepare()
    {
        string version=Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)??"0.0.0";
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SwitchportDiagramBuilder","Runtime",version);
        Directory.CreateDirectory(directory);
        foreach(string name in new[]{"WebView2Loader.dll","libSkiaSharp.dll"})
        {
            using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("SwitchportDiagramBuilder.Native."+name)
                ??throw new FileNotFoundException("Embedded runtime component is missing: "+name);
            using var buffer=new MemoryStream();resource.CopyTo(buffer);byte[] bytes=buffer.ToArray();
            string path=Path.Combine(directory,name);
            bool write=!File.Exists(path);
            if(!write){using var existing=File.OpenRead(path);write=!SHA256.HashData(existing).SequenceEqual(SHA256.HashData(bytes));}
            if(write)
            {
                string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{File.WriteAllBytes(temporary,bytes);File.Move(temporary,path,true);}finally{File.Delete(temporary);}
            }
        }
        if(!SetDllDirectory(directory))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        IntPtr webview=NativeLibrary.Load(Path.Combine(directory,"WebView2Loader.dll"));Handles.Add(webview);
        Handles.Add(NativeLibrary.Load(Path.Combine(directory,"libSkiaSharp.dll")));
        NativeLibrary.SetDllImportResolver(typeof(CoreWebView2Environment).Assembly,(name,assembly,path)=>name.Contains("WebView2Loader",StringComparison.OrdinalIgnoreCase)?webview:IntPtr.Zero);
    }
}
