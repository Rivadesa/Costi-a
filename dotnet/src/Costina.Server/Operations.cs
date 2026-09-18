using System.Text;

namespace Costina.Server;

// D5.2: salud del publicador del outbox para el diagnostico (solo main). Sin datos de negocio.
public sealed class PublisherHealth
{
    private long lastSuccess, lastFailure; private int consecutiveFailures;
    public DateTimeOffset? LastSuccessAt => Read(ref lastSuccess);
    public DateTimeOffset? LastFailureAt => Read(ref lastFailure);
    public int ConsecutiveFailures => Volatile.Read(ref consecutiveFailures);
    public void Succeeded() { Interlocked.Exchange(ref lastSuccess,DateTimeOffset.UtcNow.UtcTicks); Interlocked.Exchange(ref consecutiveFailures,0); }
    public void Failed() { Interlocked.Exchange(ref lastFailure,DateTimeOffset.UtcNow.UtcTicks); Interlocked.Increment(ref consecutiveFailures); }
    private static DateTimeOffset? Read(ref long ticks)
    {
        var value=Interlocked.Read(ref ticks);
        return value>0 ? new DateTimeOffset(value,TimeSpan.Zero) : null;
    }
}

// D5.2: un servicio de Windows no tiene consola. Log diario en <raiz de datos>\logs con retencion
// acotada. Recibe exactamente los mismos mensajes que la consola: el codigo ya no registra
// contrasenas, tokens ni cuerpos de peticion, y las suites lo verifican sobre este mismo fichero.
public sealed class FileLoggerProvider(string directory,int retainedFiles=30) : ILoggerProvider
{
    private readonly object gate=new();
    private string? currentDay;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this,categoryName);
    public void Dispose() { }

    private void Write(string line)
    {
        try
        {
            lock(gate)
            {
                var day=DateTime.UtcNow.ToString("yyyyMMdd");
                if(day!=currentDay)
                {
                    Directory.CreateDirectory(directory);
                    foreach(var old in Directory.GetFiles(directory,"server-*.log").OrderDescending().Skip(retainedFiles-1)) File.Delete(old);
                    currentDay=day;
                }
                File.AppendAllText(Path.Combine(directory,$"server-{day}.log"),line+Environment.NewLine,Encoding.UTF8);
            }
        }
        catch(Exception e) when (e is IOException or UnauthorizedAccessException) { } // el log nunca tumba el motor
    }

    private sealed class FileLogger(FileLoggerProvider owner,string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level>=LogLevel.Information;
        public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
        {
            if(!IsEnabled(level)) return;
            // Del fallo solo el tipo: un mensaje de excepcion puede arrastrar datos de conexion.
            owner.Write($"{DateTimeOffset.UtcNow:O} {level} {category}: {formatter(state,exception)}"
                +(exception is null ? "" : " ["+exception.GetType().Name+"]"));
        }
    }
}
