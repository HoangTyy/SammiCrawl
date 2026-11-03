using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace SammiCrawl.Backend.Services;

public sealed class AiQueueOptions
{
    public int MaxJobs { get; set; } = 8;
    public long MaxPayloadBytes { get; set; } = 64 * 1024 * 1024;
    public int MaxRetainedJobs { get; set; } = 256;
    public int WaitTimeoutSeconds { get; set; } = 900;
    public int ResultTtlSeconds { get; set; } = 600;
}
public sealed record AiJobError(string Code, string Error, int Status);
public sealed record AiJobSnapshot(Guid Id, string Operation, string State, int? QueuePosition,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    JsonObject? Result, AiJobError? Failure);

// Single-process FIFO. No payload or credentials are written to logs or the database.
public sealed class AiGatewayQueue(IOptions<AiQueueOptions> options, TimeProvider clock, ILogger<AiGatewayQueue> logger) : BackgroundService
{
    sealed class Job(Guid id, string operation, string fingerprint, long bytes, long sequence, DateTimeOffset created, Func<Task<JsonObject>> work)
    {
        public Guid Id = id;
        public string Operation = operation, Fingerprint = fingerprint, State = "queued";
        public long Bytes = bytes, Sequence = sequence;
        public DateTimeOffset Created = created;
        public DateTimeOffset? Started, Finished;
        public Func<Task<JsonObject>>? Work = work;
        public JsonObject? Result;
        public AiJobError? Error;
    }
    readonly object sync = new();
    readonly Channel<Job> channel = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });
    readonly Dictionary<Guid, Job> jobs = [];
    readonly AiQueueOptions limits = options.Value;
    long sequence, reservedBytes;
    int outstanding;
    bool stopping;

    public AiJobSnapshot Enqueue(Guid id, string operation, string fingerprint, long bytes, Func<Task<JsonObject>> work)
    {
        lock (sync)
        {
            Sweep();
            if (stopping) throw new AiGatewayException("AI_GATEWAY_STOPPED", "Gateway đang khởi động lại. Chưa nhận yêu cầu mới.", 503);
            if (jobs.TryGetValue(id, out var existing))
            {
                if (existing.Fingerprint != fingerprint || existing.Operation != operation)
                    throw new AiGatewayException("AI_JOB_CONFLICT", "Mã tác vụ đã dùng cho nội dung khác.", 409);
                return Snapshot(existing);
            }
            if (outstanding >= Math.Clamp(limits.MaxJobs, 1, 64) || jobs.Count >= Math.Clamp(limits.MaxRetainedJobs, 1, 4096)
                || bytes <= 0 || bytes > Math.Clamp(limits.MaxPayloadBytes, 1L, 512L * 1024 * 1024) - reservedBytes)
                throw new AiGatewayException("AI_QUEUE_FULL", "Hàng đợi AI đã đầy. Chờ bớt tác vụ rồi gửi lại.", 429);
            var job = new Job(id, operation, fingerprint, bytes, ++sequence, clock.GetUtcNow(), work);
            jobs.Add(id, job); outstanding++; reservedBytes += bytes;
            channel.Writer.TryWrite(job);
            return Snapshot(job);
        }
    }
    public AiJobSnapshot? Find(Guid id)
    {
        lock (sync) { Sweep(); return jobs.TryGetValue(id, out var job) ? Snapshot(job) : null; }
    }
    public object Status()
    {
        lock (sync)
        {
            Sweep();
            return new { waiting = jobs.Values.Count(j => j.State == "queued"), running = jobs.Values.Count(j => j.State == "running"),
                capacity = Math.Clamp(limits.MaxJobs, 1, 64), payloadBytes = reservedBytes,
                waitTimeoutSeconds = Math.Clamp(limits.WaitTimeoutSeconds, 1, 3600), resultTtlSeconds = Math.Clamp(limits.ResultTtlSeconds, 1, 3600) };
        }
    }
    // A running model is never released or declared cancelled before its real inference work finishes. Idle model residency is independent of queue ownership.
    public bool CancelQueued(Guid id)
    {
        lock (sync)
        {
            Sweep();
            if (!jobs.TryGetValue(id, out var job) || job.State != "queued") return false;
            Finish(job, "cancelled", new("AI_JOB_CANCELLED", "Đã bỏ yêu cầu đang chờ; chưa chạy AI.", 409));
            return true;
        }
    }
    AiJobSnapshot Snapshot(Job j) => new(j.Id, j.Operation, j.State,
        j.State == "queued" ? jobs.Values.Count(x => x.State == "queued" && x.Sequence < j.Sequence) + 1 : null,
        j.Created, j.Started, j.Finished, j.Result?.DeepClone().AsObject(), j.Error);
    void Finish(Job j, string state, AiJobError? error = null, JsonObject? result = null)
    {
        outstanding--; reservedBytes -= j.Bytes;
        j.State = state; j.Error = error; j.Result = result; j.Finished = clock.GetUtcNow(); j.Work = null;
    }
    void Sweep()
    {
        var now = clock.GetUtcNow();
        foreach (var j in jobs.Values)
            if (j.State == "queued" && now - j.Created >= TimeSpan.FromSeconds(Math.Clamp(limits.WaitTimeoutSeconds, 1, 3600)))
                Finish(j, "failed", new("AI_QUEUE_WAIT_TIMEOUT", "Yêu cầu hết thời gian chờ và chưa chạy AI. Gửi lại khi hàng đợi bớt tải.", 504));
        foreach (var id in jobs.Where(p => p.Value.Finished is { } end && now - end >= TimeSpan.FromSeconds(Math.Clamp(limits.ResultTtlSeconds, 1, 3600))).Select(p => p.Key).ToArray()) jobs.Remove(id);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var j in channel.Reader.ReadAllAsync(stoppingToken))
            {
                Func<Task<JsonObject>> work;
                lock (sync)
                {
                    Sweep();
                    if (j.State != "queued" || j.Work is null) continue;
                    j.State = "running"; j.Started = clock.GetUtcNow(); work = j.Work;
                }
                try
                {
                    var result = await work();
                    lock (sync) Finish(j, "completed", result: result);
                }
                catch (Exception ex)
                {
                    var error = ex is UnchangedRewriteException ? new AiGatewayException("AI_UNCHANGED_OUTPUT", "AI trả lại nội dung gốc, chưa tạo bản viết lại.", 422)
                        : ex is AiModelUnavailableException ? new AiGatewayException("AI_MODEL_UNAVAILABLE", "Máy chủ AI thiếu model hoặc chưa sẵn sàng.", 503)
                        : AiGatewayException.FromException(ex);
                    logger.LogWarning("AI job {JobId} failed: {Code}; type {ExceptionType}", j.Id, error.Code, ex.GetType().Name);
                    lock (sync) Finish(j, "failed", new(error.Code, error.Message, error.Status));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            stopping = true; channel.Writer.TryComplete();
            foreach (var j in jobs.Values.Where(j => j.State == "queued"))
                Finish(j, "failed", new("AI_GATEWAY_STOPPED", "Gateway dừng trước khi chạy yêu cầu. Không tự chạy lại.", 503));
        }
        await base.StopAsync(cancellationToken);
    }
}
