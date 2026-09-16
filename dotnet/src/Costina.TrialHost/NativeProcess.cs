using System.Diagnostics;
namespace Costina.TrialHost;

internal static class NativeProcess
{
    public static ProcessStartInfo Info(string file,IEnumerable<string> args,string work,IDictionary<string,string>? environment=null)
    {
        var psi=new ProcessStartInfo(Path.GetFullPath(file)) {WorkingDirectory=work,UseShellExecute=false,CreateNoWindow=true};
        foreach(var arg in args) psi.ArgumentList.Add(arg);
        // Never leak DB or other trial role credentials inherited from a parent process.
        foreach(var key in psi.Environment.Keys.Where(k=>k.StartsWith("COSTINA_",StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("PG",StringComparison.OrdinalIgnoreCase)).ToArray()) psi.Environment.Remove(key);
        if(environment is not null) foreach(var pair in environment) psi.Environment[pair.Key]=pair.Value;
        return psi;
    }
    public static async Task<(int Code,string Output)> Run(string file,IEnumerable<string> args,string work,
        IDictionary<string,string>? environment=null,int timeoutSeconds=60,bool allowFailure=false)
    {
        var psi=Info(file,args,work,environment); psi.RedirectStandardError=psi.RedirectStandardOutput=true;
        using var child=Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar un componente nativo.");
        var stdout=child.StandardOutput.ReadToEndAsync(); var stderr=child.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try {await child.WaitForExitAsync(timeout.Token);} catch(OperationCanceledException) {
            // Only this exact owned utility is stopped; never terminate a process found by name/port.
            if(!child.HasExited) child.Kill();
            throw new TimeoutException($"{Path.GetFileName(file)} no terminó. Conserva los datos y revisa el diagnóstico.");
        }
        var output=(await stdout)+"\n"+(await stderr);
        if(child.ExitCode!=0 && !allowFailure)
            throw new InvalidOperationException($"{Path.GetFileName(file)} terminó con código {child.ExitCode}. No se han borrado los datos.\n" + System.Text.RegularExpressions.Regex.Replace(output,"[A-Fa-f0-9]{64}","[redacted]"));
        return(child.ExitCode,output);
    }
}
