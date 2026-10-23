namespace SammiCrawl.Backend.Contracts;

public record RewriteRequest(string Text, string Tone = "engaging", string Mode = "rewrite", string Instructions = "", double Temperature = 0.7);
