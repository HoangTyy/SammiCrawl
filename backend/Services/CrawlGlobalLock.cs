using System.Data.Common;
using Npgsql;
using Microsoft.Data.SqlClient;

namespace SammiCrawl.Backend.Services;

// Dedicated, nonpooled session: ownership lasts for the entire browser run and AI cleanup.
public sealed class CrawlGlobalLock(IConfiguration config)
{
    const long Resource = 7361677381149201L;
    public async Task<CrawlGlobalLease?> TryAcquire(CancellationToken ct)
    {
        var postgres=!(config["DatabaseProvider"]??"PostgreSql").Equals("SqlServer",StringComparison.OrdinalIgnoreCase);
        var name="SammiCrawl.CrawlLock:"+Guid.NewGuid().ToString("N");
        DbConnection connection;
        if(postgres)
        {
            var settings=new NpgsqlConnectionStringBuilder(config.GetConnectionString("Database")){Pooling=false,Timeout=5,CommandTimeout=5,KeepAlive=5,ApplicationName=name};
            connection=new NpgsqlConnection(settings.ConnectionString);
        }
        else
        {
            var settings=new SqlConnectionStringBuilder(config.GetConnectionString("Database")){Pooling=false,ConnectTimeout=5,ApplicationName=name};
            connection=new SqlConnection(settings.ConnectionString);
        }
        try
        {
            await connection.OpenAsync(ct);
            await using var command=connection.CreateCommand();command.CommandTimeout=5;
            command.CommandText=postgres?$"SELECT pg_try_advisory_lock({Resource})":"DECLARE @r int; EXEC @r=sp_getapplock @Resource='sammicrawl-facebook-global', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;";
            var result=await command.ExecuteScalarAsync(ct);
            if(postgres?result is not true:Convert.ToInt32(result)<0){await connection.DisposeAsync();return null;}
            return new CrawlGlobalLease(connection,name,postgres);
        }
        catch{await connection.DisposeAsync();throw;}
    }
}
public sealed class CrawlGlobalLease: IAsyncDisposable
{
    readonly DbConnection connection;
    readonly CancellationTokenSource monitorStop=new(),lost=new();
    readonly Task monitor;
    readonly SemaphoreSlim io=new(1,1);
    readonly bool postgres;
    public string SessionName {get;}
    public CancellationToken LostToken=>lost.Token;
    public void ThrowIfLost()=>LostToken.ThrowIfCancellationRequested();
    internal CrawlGlobalLease(DbConnection connection,string name,bool postgres){this.connection=connection;this.postgres=postgres;SessionName=name;monitor=Monitor();}
    public async Task<DateTimeOffset> DatabaseTime(CancellationToken ct)
    {
        ThrowIfLost();await io.WaitAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();command.CommandText=postgres?"SELECT CURRENT_TIMESTAMP":"SELECT SYSUTCDATETIME()";command.CommandTimeout=3;
            var value=await command.ExecuteScalarAsync(ct);
            return value is DateTimeOffset offset?offset:new DateTimeOffset(DateTime.SpecifyKind((DateTime)value!,DateTimeKind.Utc));
        }
        catch{if(!ct.IsCancellationRequested)lost.Cancel();throw;}
        finally{io.Release();}
    }
    async Task Monitor()
    {
        try
        {
            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2),monitorStop.Token);
                await io.WaitAsync(monitorStop.Token);
                try{await using var command=connection.CreateCommand();command.CommandText="SELECT 1";command.CommandTimeout=3;await command.ExecuteScalarAsync(monitorStop.Token);}
                finally{io.Release();}
            }
        }
        catch(OperationCanceledException) when(monitorStop.IsCancellationRequested){}
        catch{lost.Cancel();}
    }
    public async ValueTask DisposeAsync()
    {
        monitorStop.Cancel();await monitor;
        // Closing a nonpooled session releases its advisory/application lock, even after errors.
        await connection.DisposeAsync();monitorStop.Dispose();lost.Dispose();io.Dispose();
    }
}
