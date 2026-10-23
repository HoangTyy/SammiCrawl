namespace SammiCrawl.Backend.Contracts;

public record RuntimeAccountError(Guid JobId, string Operation, string Code);
