using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using SwitchportGenerator;
namespace SwitchportDiagramBuilder;
public sealed partial class MainForm : Form
{
    readonly WebView2 web = new() { Dock = DockStyle.Fill };
    readonly string root = AppContext.BaseDirectory;
    readonly string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwitchportDiagramBuilder");
    bool busy;
    string outputRoot="";
    string? lastOutput;
    string? lastChart;
    string? selectedPath;
    string? selectedText;
    List<DeviceIdentity> deviceDetails=new();
    const int MaxInputBytes=20_000_000;
    ReportLogo? reportLogo;
    string logoName="Report logo";
    string LogoPath=>Path.Combine(data,"report-logo.png");
    public MainForm(string[]? launchArgs=null)
    {
        Text = "Switchport Diagram Builder";
        using(var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SwitchportDiagramBuilder.AppIcon.ico"))
        {
            if(iconStream!=null){using var embeddedIcon=new Icon(iconStream);Icon=(Icon)embeddedIcon.Clone();}
        }
        ShowIcon=true;
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;
        Width=Math.Min(1180,area.Width-32);Height=Math.Min(760,area.Height-32);StartPosition=FormStartPosition.CenterScreen;
        MinimumSize = new(900, 650); Controls.Add(web);
        outputRoot=Path.Combine(data,"Output");
        InitializeUpdates(launchArgs??Array.Empty<string>());
        Shown += async (_, _) => await Init();
    }
    async Task Init()
    {
        try
        {
            Directory.CreateDirectory(data);
            string preference=Path.Combine(data,"output-folder.txt");
            if(File.Exists(preference)){string saved=File.ReadAllText(preference).Trim();if(Path.IsPathFullyQualified(saved)&&Directory.Exists(saved))outputRoot=saved;}
            if(File.Exists(LogoPath))try
            {
                reportLogo=ReportLogo.Load(LogoPath);
                string nameFile=Path.Combine(data,"report-logo-name.txt");
                if(File.Exists(nameFile))logoName=File.ReadAllText(nameFile);
            }catch{reportLogo=null;}
            // Extract embedded assets into the writable data directory. The release also
            // includes wwwroot for review, but startup never depends on the working directory.
            string assets = Path.Combine(data, "UI", AppUpdateController.CurrentVersionText);
            Directory.CreateDirectory(assets);
            var assembly = Assembly.GetExecutingAssembly();
            foreach (string name in new[] { "index.html", "app.css", "app.js", "app-icon.svg" })
            {
                using var source = assembly.GetManifestResourceStream("SwitchportDiagramBuilder.wwwroot." + name)
                    ?? throw new FileNotFoundException("Embedded interface file is missing: " + name);
                using var target = File.Create(Path.Combine(assets, name));
                source.CopyTo(target);
            }
            string runtime = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(runtime)) throw new InvalidOperationException("Microsoft Edge WebView2 Runtime is not installed.");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(data, "WebView2"));
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("switchport.local", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.WebMessageReceived += Message;
            web.CoreWebView2.NavigationStarting += (_,args) =>
            {
                if(!Uri.TryCreate(args.Uri,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="switchport.local")args.Cancel=true;
            };
            web.Source = new Uri("https://switchport.local/index.html");
        }
        catch (Exception ex)
        {
            try { Directory.CreateDirectory(data); File.WriteAllText(Path.Combine(data, "startup-error.txt"), ex.ToString()); } catch { }
            string detail = ex is WebView2RuntimeNotFoundException
                ? "Install Microsoft Edge WebView2 Runtime (x64), then reopen the app."
                : "Confirm that Microsoft Edge WebView2 Runtime is installed and your Local AppData folder is writable. Re-download the executable if its embedded files are damaged.";
            MessageBox.Show(this, "The interface could not start.\n\n" + ex.Message + "\n\n" + detail + "\n\nDiagnostic log: " + Path.Combine(data, "startup-error.txt"), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
    async void Message(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!Uri.TryCreate(e.Source,UriKind.Absolute,out var source)||source.Scheme!="https"||source.Host!="switchport.local")return;
        try
        {
            var message = JsonSerializer.Deserialize<Msg>(e.WebMessageAsJson);
            if(message?.action.StartsWith("update",StringComparison.Ordinal)==true){await UpdateAction(message);return;}
            if(updater?.Installing==true)return;
            if (message?.action == "choose" && !busy) Choose();
            else if (message?.action == "loadText" && !busy) SelectText(message.text??"",message.name??"Pasted configuration");
            else if (message?.action == "loadBackup" && !busy) SelectText(ConfigInput.Decode(Convert.FromBase64String(message.text??"")),message.name??"RouterOS backup");
            else if (message?.action == "chooseOutput" && !busy) ChooseOutput();
            else if (message?.action == "chooseLogo" && !busy) ChooseLogo();
            else if (message?.action == "removeLogo" && !busy) RemoveLogo();
            else if (message?.action is "inspectDevices" or "saveDevices" && !busy) DeviceAction(message);
            else if (message?.action == "ready")
            {
                Send(new {type="outputSelected",path=outputRoot});
                Send(new {type="models",models=DeviceModels.Choices.Select(m=>new{sku=m.Sku,name=m.Name,kind=m.Kind}).ToArray()});
                SendLogo();
                RestoreUpdateSession();
                UpdatesReady();
            }
            else if (message?.action == "open") Open();
            else if (message?.action == "copyOutput") CopyOutput(message.text??"");
            else if (message?.action == "openChart" && !busy) OpenChart();
            else if (message?.action == "run" && !busy) await Run();
        }
        catch (Exception ex) { Send(new { type = "error", message = ex.Message }); }
    }
    void Choose()
    {
        using var dialog = new OpenFileDialog { Filter = "Switch configurations|*.txt;*.cfg;*.conf;*.log;*.rsc;*.backup|All files|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            if(new FileInfo(dialog.FileName).Length>MaxInputBytes)throw new InvalidOperationException("Configuration exceeds the 20 MB limit.");
            string text=ConfigInput.Read(dialog.FileName);
            if(string.IsNullOrWhiteSpace(text))throw new InvalidOperationException("The configuration is empty.");
            selectedPath=dialog.FileName;selectedText=null;
            deviceDetails.Clear();
            lastChart=null;lastOutput=null;
            Send(new { type = "selected", path = dialog.FileName, name = Path.GetFileName(dialog.FileName),hostname=ReportText.ReadHostname(text) });
        }
    }
    void SelectText(string text,string name)
    {
        if(string.IsNullOrWhiteSpace(text))throw new InvalidOperationException("Paste or drop a configuration that contains text.");
        if(System.Text.Encoding.UTF8.GetByteCount(text)>MaxInputBytes)throw new InvalidOperationException("Configuration exceeds the 20 MB limit.");
        selectedText=text;selectedPath=null;lastChart=null;lastOutput=null;
        deviceDetails.Clear();
        string display=string.IsNullOrWhiteSpace(name)?"Pasted configuration":name;
        Send(new {type="selected",path="",name=display[..Math.Min(display.Length,200)],hostname=ReportText.ReadHostname(text)});
    }
    string SelectedSource()
    {
        return selectedText??(selectedPath!=null&&File.Exists(selectedPath)?ConfigInput.Read(selectedPath):throw new InvalidOperationException("Load a configuration first."));
    }
    void DeviceAction(Msg message)
    {
        try
        {
            string source=SelectedSource();
            var inv=DeviceModels.Parse(source,message.action=="saveDevices"?(message.devices??Array.Empty<DeviceIdentity>()):deviceDetails);
            if(message.action=="saveDevices")
            {
                deviceDetails=inv.Devices.Where(d=>d.HasDetails).ToList();lastOutput=null;lastChart=null;
                Send(new {type="devicesSaved",count=deviceDetails.Count,requestId=message.requestId});
            }
            else Send(new {type="devices",devices=inv.Devices,requestId=message.requestId});
        }
        catch(Exception ex){Send(new {type="deviceError",message=ex.Message,requestId=message.requestId});}
    }
    void ChooseOutput()
    {
        using var dialog=new FolderBrowserDialog{Description="Choose where generated report packages are saved",UseDescriptionForTitle=true,SelectedPath=outputRoot,ShowNewFolderButton=true};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        string selected=Path.GetFullPath(dialog.SelectedPath);
        File.WriteAllText(Path.Combine(data,"output-folder.txt"),selected);
        outputRoot=selected;lastOutput=null;lastChart=null;
        Send(new {type="outputSelected",path=outputRoot});
    }
    void SendLogo()=>Send(new {type="logoSelected",name=reportLogo==null?"":logoName,preview=reportLogo==null?"":"data:image/png;base64,"+Convert.ToBase64String(reportLogo.PngData)});
    void ChooseLogo()
    {
        using var dialog=new OpenFileDialog{Title="Choose report logo",Filter="Logo images (PNG, JPG, WebP)|*.png;*.jpg;*.jpeg;*.webp",CheckFileExists=true};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        var logo=ReportLogo.Load(dialog.FileName);
        string temporary=Path.Combine(data,Guid.NewGuid().ToString("N")+"-logo.png");
        try{File.WriteAllBytes(temporary,logo.PngData);File.Move(temporary,LogoPath,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
        reportLogo=logo;logoName=Path.GetFileName(dialog.FileName);
        File.WriteAllText(Path.Combine(data,"report-logo-name.txt"),logoName);
        lastChart=null;lastOutput=null;SendLogo();
    }
    void RemoveLogo()
    {
        File.Delete(LogoPath);File.Delete(Path.Combine(data,"report-logo-name.txt"));
        reportLogo=null;lastChart=null;lastOutput=null;SendLogo();
    }
    void CopyOutput(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        try
        {
            Clipboard.SetText(text,TextDataFormat.UnicodeText);
            Send(new {type="outputCopied",message="Copied."});
        }
        catch(System.Runtime.InteropServices.ExternalException)
        {
            Send(new {type="outputCopyFailed",message="Clipboard is busy. Try Copy again."});
        }
    }
    void Open()
    {
        string output = lastOutput??outputRoot; Directory.CreateDirectory(output);
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { output } });
    }
    void OpenChart()
    {
        if(lastChart==null||!File.Exists(lastChart))
        {
            lastChart=null;
            Send(new {type="chartUnavailable",message="The chart is unavailable. Build reports again."});
            return;
        }
        Process.Start(new ProcessStartInfo(lastChart){UseShellExecute=true});
    }
    async Task Run()
    {
        if (selectedText==null&&!File.Exists(selectedPath)) { Send(new { type = "error", message = "Choose, paste or drop a configuration first." }); return; }
        string runner = Environment.ProcessPath??throw new InvalidOperationException("Application path is unavailable.");
        busy = true;lastOutput=null;lastChart=null;
        string? temporaryInput=null;
        string? temporaryDetails=null;
        try
        {
            string input=selectedPath??"";
            if(selectedText!=null)
            {
                string inputs=Path.Combine(data,"Input");Directory.CreateDirectory(inputs);
                input=temporaryInput=Path.Combine(inputs,Guid.NewGuid().ToString("N")+".txt");
                await File.WriteAllTextAsync(input,selectedText,new System.Text.UTF8Encoding(false));
            }
            string output = outputRoot;
            Send(new { type = "status", message = "Validating configuration and building diagrams..." });
            var start = new ProcessStartInfo(runner) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            start.ArgumentList.Add("--generate");start.ArgumentList.Add("--input"); start.ArgumentList.Add(input); start.ArgumentList.Add("--output"); start.ArgumentList.Add(output);start.ArgumentList.Add("--named-folder");
            if(reportLogo!=null){start.ArgumentList.Add("--logo");start.ArgumentList.Add(LogoPath);}
            if(deviceDetails.Count>0)
            {
                string inputs=Path.Combine(data,"Input");Directory.CreateDirectory(inputs);
                temporaryDetails=Path.Combine(inputs,Guid.NewGuid().ToString("N")+"-devices.json");
                await File.WriteAllTextAsync(temporaryDetails,JsonSerializer.Serialize(deviceDetails));
                start.ArgumentList.Add("--device-details");start.ArgumentList.Add(temporaryDetails);
            }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("The generator could not start.");
            // Drain both streams concurrently, avoiding a full stderr pipe blocking the child.
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string normal=await stdout;string error=await stderr;
            string? generated=normal.Split('\n').FirstOrDefault(line=>line.StartsWith("OUTPUT_DIRECTORY=",StringComparison.Ordinal));
            if(generated!=null)
            {
                string actual=generated["OUTPUT_DIRECTORY=".Length..].TrimEnd('\r');
                string parent=Path.GetDirectoryName(Path.GetFullPath(actual))??"";
                if(parent.TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)&&Directory.Exists(actual))
                    output=lastOutput=actual;
            }
            string text = process.ExitCode == 0 ? string.Join('\n',normal.Split('\n').Where(line=>!line.StartsWith("OUTPUT_DIRECTORY=",StringComparison.Ordinal))).Trim() : error;
            if (process.ExitCode == 0)
            {
                string? chart=lastOutput==null?null:Path.Combine(lastOutput,"wall-chart.png");
                lastChart=chart!=null&&File.Exists(chart)?chart:null;
                Send(new { type = "complete", message = "Reports created.", details = "", output,chartAvailable=lastChart!=null });
            }
            else Send(new { type = "blocked", message = "Generation blocked. Review the validation report.", details = text, output });
        }
        finally
        {
            if(temporaryInput!=null)try{File.Delete(temporaryInput);}catch(IOException){}catch(UnauthorizedAccessException){}
            if(temporaryDetails!=null)try{File.Delete(temporaryDetails);}catch(IOException){}catch(UnauthorizedAccessException){}
            busy = false;
            updater?.Publish();
        }
    }
    void Send(object value)
    {
        if (!IsDisposed && web.CoreWebView2 != null) web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(value));
    }
    sealed record Msg(string action, string? path,string? text,string? name,DeviceIdentity[]? devices,int? requestId,bool? enabled=null);
}
