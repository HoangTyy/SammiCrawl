using System.Text.Json.Nodes;
namespace SammiCrawl.Backend.Services;

public sealed class TaskControlService(IProjectRepository db, FacebookCrawlWorker worker, AiRunner ai)
{
    public async Task<ServiceResult> Pause(Guid id)
    {
        var doc = await db.FindDocumentAsync(id,"tasks"); if(doc is null)return ServiceResult.NotFound();
        var data=JsonNode.Parse(doc.Json)!.AsObject();
        if(data["status"]?.ToString() is not ("pending" or "running"))return ServiceResult.Conflict(new{error="Lượt này không đang chạy hoặc chờ."});
        var queued=data["status"]!.ToString()=="pending";
        data["status"]="paused";data["pauseAcknowledged"]=queued;
        data["errorCode"]="USER_PAUSED";data["errorReason"]=queued?"Đã tạm dừng lượt đang chờ.":"Đang dừng: chờ thao tác hiện tại kết thúc và giải phóng AI.";
        return await db.TryUpdateTaskAsync(id,doc.Json,data.ToJsonString(),$"Yêu cầu tạm dừng tác vụ {id}")?ServiceResult.Ok(data):ServiceResult.Conflict(new{error="Trạng thái vừa thay đổi. Tải lại danh sách."});
    }
    public async Task<ServiceResult> Resume(Guid id)
    {
        var doc=await db.FindDocumentAsync(id,"tasks");if(doc is null)return ServiceResult.NotFound();
        var data=JsonNode.Parse(doc.Json)!.AsObject();
        if(data["status"]?.ToString() is not ("paused" or "failed"))return ServiceResult.Conflict(new{error="Chỉ tiếp tục lượt tạm dừng hoặc lỗi."});
        if(data["pauseAcknowledged"]?.GetValue<bool>()==false)return ServiceResult.Conflict(new{error="Worker chưa dừng xong. Chờ thao tác hiện tại hoàn tất rồi tiếp tục."});
        if(!Guid.TryParse(data["brandId"]?.ToString(),out var brandId)||await db.FindDocumentAsync(brandId,"brands") is not { } brand||JsonNode.Parse(brand.Json)?["deletedAt"] is not null)return ServiceResult.BadRequest(new{error="Thương hiệu không còn hoạt động."});
        var eligible=(await db.ListDocumentsAsync("accounts")).Where(d=>{var a=JsonNode.Parse(d.Json);return a?["deletedAt"] is null&&a?["role"]?.ToString()=="crawl"&&a?["enabled"]?.GetValue<bool>()!=false&&a?["facebookStatus"]?.ToString()=="active"&&!string.IsNullOrEmpty(d.ProtectedSecret);}).Select(a=>a.Id).ToHashSet();
        if(CrawlAccountSelection.Validate(data["crawlAccountIds"],eligible,out _) is string error)return ServiceResult.BadRequest(new{error});
        if(CrawlEvaluationState.ReadinessError(System.Text.Json.JsonSerializer.SerializeToNode(await ai.Status())) is string aiError)return ServiceResult.Json(new{error=aiError},503);
        data["status"]="pending";data["workerNode"]="Facebook local";data["executionToken"]=Guid.NewGuid().ToString();data["pauseAcknowledged"]=true;
        data["errorCode"]=null;data["errorReason"]="Đã xếp hàng tiếp tục; giữ tiến độ và chống trùng bài cũ.";
        if(!await db.TryUpdateTaskAsync(id,doc.Json,data.ToJsonString(),$"Xếp hàng tiếp tục tác vụ {id}"))return ServiceResult.Conflict(new{error="Lượt đã được máy khác thay đổi hoặc tiếp tục. Tải lại danh sách."});
        if(!worker.Enqueue(id,data["executionToken"]!.ToString()))return ServiceResult.Conflict(new{error="Không thêm được vào hàng đợi."});
        return ServiceResult.Ok(data);
    }
}
