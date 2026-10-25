using SwitchportGenerator;
namespace SwitchportDiagramBuilder;
internal static class Program
{
    [System.Runtime.InteropServices.DllImport("shell32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [STAThread]
    static int Main(string[] args)
    {
        bool generation=args.Length>0&&args[0]=="--generate";
        try
        {
            if(args.Length==2&&args[0]=="--apply-update")return UpdateApplyHost.RunAsync(args[1]).GetAwaiter().GetResult();
            if(args.Length==1&&args[0]=="--updater-self-test")return UpdateHelperSelfTest.RunAsync().GetAwaiter().GetResult();
            if(args.Length>0&&args[0].StartsWith("--updater-selftest-",StringComparison.Ordinal))return UpdateHelperSelfTest.Child(args);
            NativeRuntime.Prepare();
            if(generation)return GeneratorEntry.Run(args[1..]);
            SetCurrentProcessExplicitAppUserModelID("Switchport.DiagramBuilder");
            ApplicationConfiguration.Initialize();Application.Run(new MainForm(args));return 0;
        }
        catch(Exception ex)
        {
            try
            {
                string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SwitchportDiagramBuilder");
                Directory.CreateDirectory(logs);File.WriteAllText(Path.Combine(logs,"startup-error.txt"),ex.ToString());
            }
            catch { }
            string message="The application could not initialize its embedded runtime files. Confirm that your Local AppData folder is writable.\n\n"+ex.Message;
            if(generation||args.Any(a=>a.StartsWith("--updater-",StringComparison.Ordinal)||a=="--apply-update"))Console.Error.WriteLine(message);
            else MessageBox.Show(message,"Switchport Diagram Builder",MessageBoxButtons.OK,MessageBoxIcon.Error);
            return 4;
        }
    }
}
