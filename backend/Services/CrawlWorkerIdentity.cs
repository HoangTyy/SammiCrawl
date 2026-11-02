namespace SammiCrawl.Backend.Services;
public sealed class CrawlWorkerIdentity
{
    public string Id {get;}
    public CrawlWorkerIdentity(IConfiguration config)
    {
        if(!string.IsNullOrWhiteSpace(config["Crawl:WorkerId"])) {Id=config["Crawl:WorkerId"]!;return;}
        var path=config["Crawl:WorkerIdentityPath"]??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".aspnet","DataProtection-Keys","sammicrawl-worker-id");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        try{using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);using var writer=new StreamWriter(stream);writer.Write(Guid.NewGuid().ToString("N"));}
        catch(IOException) when(File.Exists(path)){}
        Id=File.ReadAllText(path).Trim();if(!Guid.TryParse(Id,out _))throw new InvalidOperationException("Worker identity file không hợp lệ.");
    }
}
