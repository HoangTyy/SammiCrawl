using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using SammiCrawl.Backend.Services;
using SammiCrawl.Backend.Repositories;
using Microsoft.AspNetCore.DataProtection;
namespace SammiCrawl.Backend.Controllers;
[ApiController,Authorize]
public sealed class HermesController(IProjectRepository db,AiRunner ai,AiGatewayQueue queue,CrawlGlobalLock crawlLock,IDataProtectionProvider protection):ControllerBase
{
 public sealed record LearnRequest(Guid AccountId,string ExcelContext,string ExcelUrl);
 public sealed record SearchProbeRequest(Guid AccountId,string Keyword);
 [HttpPost("/api/facebook/search-probe")]
 public async Task<IActionResult> SearchProbe(SearchProbeRequest request)
 {
  if(string.IsNullOrWhiteSpace(request.Keyword)||request.Keyword.Length>120)return BadRequest(new{error="Từ khóa cần dài 1–120 ký tự."});
  var account=await db.FindDocumentAsync(request.AccountId,"accounts");var a=account is null?null:JsonNode.Parse(account.Json);
  if(account?.ProtectedSecret is null||a?["deletedAt"] is not null||a?["role"]?.ToString()!="crawl"||a?["facebookStatus"]?.ToString()!="active"||a?["enabled"]?.GetValue<bool>()==false)return BadRequest(new{error="Cần tài khoản cào hoạt động có phiên Facebook."});
  await using var lease=await crawlLock.TryAcquire(HttpContext.RequestAborted);
  if(lease is null)return Conflict(new{error="Đang có lượt cào; chưa kiểm tra tìm nhóm."});
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted,lease.LostToken);timeout.CancelAfter(TimeSpan.FromSeconds(70));
  try{var result=await new FacebookScopeReader(protection).Search(account.ProtectedSecret,request.Keyword.Trim(),timeout.Token);lease.ThrowIfLost();return Ok(result);}
  catch(OperationCanceledException){return StatusCode(504,new{error="Kiểm tra đã dừng hoặc hết thời gian; không tạo lượt cào."});}
  catch(Exception){return StatusCode(502,new{error="Không đọc được tìm kiếm bằng phiên tài khoản; kiểm tra phiên hoặc giới hạn Facebook."});}
 }
 [HttpPost("/api/brands/{id:guid}/learn-scope")]
 public async Task<IActionResult> Learn(Guid id,LearnRequest request)
 {
  if(!ai.HermesEnabled)return StatusCode(503,new{error="Hermes chưa bật."});
  if(string.IsNullOrWhiteSpace(request.ExcelContext)||request.ExcelContext.Length>7000||!Uri.TryCreate(request.ExcelUrl,UriKind.Absolute,out var sheet)||sheet.Scheme!="https"||sheet.Host!="docs.google.com")return BadRequest(new{error="Cần nội dung Excel đã đọc, tối đa 7000 ký tự và link Google Sheets."});
  var row=await db.FindDocumentAsync(id,"brands");if(row is null)return NotFound();var brand=JsonNode.Parse(row.Json)!.AsObject();if(brand["deletedAt"] is not null)return NotFound();
  var account=await db.FindDocumentAsync(request.AccountId,"accounts");var a=account is null?null:JsonNode.Parse(account.Json);
  if(account?.ProtectedSecret is null||a?["deletedAt"] is not null||a?["role"]?.ToString()!="crawl"||a?["facebookStatus"]?.ToString()!="active"||a?["enabled"]?.GetValue<bool>()==false)return BadRequest(new{error="Chọn tài khoản cào đang hoạt động, có phiên Facebook."});
  var urls=BrandPolicy.Channels(brand).Where(c=>c?["active"]?.GetValue<bool>()!=false&&BrandPolicy.FacebookUrl(c?["url"]?.ToString(),c?["type"]?.ToString()??"group")).OrderBy(c=>c?["type"]?.ToString()=="group"?0:1).Take(1).Select(c=>c!["url"]!.ToString()).ToArray();
  if(urls.Length==0)return BadRequest(new{error="Kênh đích chưa có URL hợp lệ."});
  JsonArray references;
  await using(var lease=await crawlLock.TryAcquire(HttpContext.RequestAborted))
  {
   if(lease is null)return Conflict(new{error="Đang có lượt cào; chờ hoàn tất trước khi đọc bài tham chiếu."});
   try{references=await new FacebookScopeReader(protection).Read(account.ProtectedSecret,urls,HttpContext.RequestAborted);lease.ThrowIfLost();}
   catch(OperationCanceledException){throw;}
   catch(Exception){return StatusCode(502,new{error="Chưa đọc được bài tham chiếu Facebook; kiểm tra phiên/quyền truy cập kênh. Chưa lưu scope."});}
  }
  if(references.Count==0)return StatusCode(422,new{error="Chưa có bài công khai đọc được để tham chiếu; không giả lập đã học kênh."});
  var payload=HermesAdvice.Input(new JsonObject{["profile"]=SourceDiscovery.Profile(brand),["excel"]=request.ExcelContext,["references"]=references.DeepClone(),["priority"]="Yêu cầu mới/cấu hình hiện tại cao hơn Excel cũ và bài lịch sử. Tên nhóm và đối tượng học sinh sinh viên giúp mở rộng hợp lý, không bắt chước 100% bài cũ. Bài hiện diện không chứng minh duyệt tay; adminMarker chỉ là nhãn nhìn thấy. Không bịa dữ liệu hoặc nguồn."});
  var job=queue.Enqueue(Guid.NewGuid(),"hermes-scope",Guid.NewGuid().ToString(),System.Text.Encoding.UTF8.GetByteCount(payload),async()=>{await ai.AcquireAsync();try{return HermesAdvice.Parse(await ai.Rewrite(new RewriteRequest(payload,Mode:"hermes-config",Temperature:0.2)));}finally{ai.Release();}});
  try{while(job.State is "queued" or "running"){await Task.Delay(500,HttpContext.RequestAborted);job=queue.Find(job.Id)??throw new InvalidOperationException();}}
  catch(OperationCanceledException)when(HttpContext.RequestAborted.IsCancellationRequested){queue.CancelQueued(job.Id);throw;}
  if(job.State!="completed")return StatusCode(job.Failure?.Status??502,new{error=job.Failure?.Error??"Hermes chưa hoàn tất."});
  var learned=job.Result!.DeepClone().AsObject();learned["excelUrl"]=request.ExcelUrl;learned["excelContext"]=request.ExcelContext;learned["references"]=references;learned["trainedAt"]=DateTimeOffset.UtcNow.ToString("O");learned["method"]="reference-scope-not-model-training";learned["applied"]=true;
  var updated=brand.DeepClone().AsObject();updated["learnedScope"]=learned;updated["scopeExcelContext"]=request.ExcelContext;updated["scopeExcelUrl"]=request.ExcelUrl;updated["updatedAt"]=DateTimeOffset.UtcNow.ToString("O");
  if(!await db.TryUpdateBrandAsync(id,row.Json,updated.ToJsonString(),$"Hermes tạo scope từ Excel và {references.Count} bài tham chiếu · {id}"))return Conflict(new{error="Hồ sơ đã đổi trong khi học; scope chưa lưu, tải lại trước khi thử."});
  return Ok(learned);
 }
 [HttpPost("/api/brands/{id:guid}/hermes-analysis")]
 public async Task<IActionResult> Analyze(Guid id)
 {
  if(!ai.HermesEnabled)return StatusCode(503,new{error="Hermes chưa được bật trên máy chủ AI."});
  var row=await db.FindDocumentAsync(id,"brands");if(row is null)return NotFound();
  var brand=JsonNode.Parse(row.Json)!;if(brand["deletedAt"] is not null)return NotFound();
  var profile=SourceDiscovery.Profile(brand);
  var samples=(await db.ListDocumentsAsync("articles")).Select(r=>JsonNode.Parse(r.Json)!).Where(a=>a["brandId"]?.ToString()==id.ToString()).Take(12).Select(a=>new JsonObject{["title"]=a["title"]?.ToString()?[..Math.Min(160,a["title"]!.ToString().Length)],["status"]=a["aiStatus"]?.ToString(),["score"]=a["aiScore"]?.DeepClone(),["sourceGroup"]=a["sourceGroupName"]?.ToString()});
  var payload=HermesAdvice.Input(new JsonObject{["profile"]=profile,["recentArticles"]=new JsonArray(samples.ToArray())});
  AiJobSnapshot job;
  try{job=queue.Enqueue(Guid.NewGuid(),"hermes-analysis",Guid.NewGuid().ToString(),System.Text.Encoding.UTF8.GetByteCount(payload),async()=>{await ai.AcquireAsync();try{return HermesAdvice.Parse(await ai.Rewrite(new RewriteRequest(payload,Mode:"hermes-config",Temperature:0.2)));}finally{ai.Release();}});}
  catch(AiGatewayException ex){return StatusCode(ex.Status,new{error=ex.Message});}
  try{
   while(job.State is "queued" or "running"){await Task.Delay(500,HttpContext.RequestAborted);job=queue.Find(job.Id)??throw new InvalidOperationException();}
   return job.State=="completed"?Ok(job.Result):StatusCode(job.Failure?.Status??502,new{error=job.Failure?.Error??"Hermes chưa hoàn thành."});
  }catch(OperationCanceledException) when(HttpContext.RequestAborted.IsCancellationRequested){queue.CancelQueued(job.Id);throw;}
 }
}
