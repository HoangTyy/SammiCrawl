using System.Text.Json;
using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

public interface IContentService
{
    Task<ServiceResult> ReadVisualContext(Guid id);
    Task<ServiceResult> CreateDraft(Guid id, JsonObject request);
    Task<ServiceResult> SaveDraft(Guid id, JsonObject request);
    Task<ServiceResult> MarkArticlePublished(Guid id,JsonObject request,string actor);
    Task<ServiceResult> MarkPublished(Guid id,JsonObject request,string actor);
    Task<ServiceResult> ApproveDraft(Guid id, JsonObject request, string reviewer);
    Task<ServiceResult> EvaluateArticle(Guid id);
    Task<ServiceResult> Rewrite(RewriteRequest r);
}

public sealed class ContentService(IProjectRepository db, AiRunner ai, IWebHostEnvironment environment, ILogger<ContentService> logger) : IContentService
{
    static string VisualFingerprint(string source, List<string> images) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source + "\n" + string.Join("\n", images))));
    async Task<List<string>> ReadSourceImages(JsonNode article)
    {
        if ((article["mediaWarnings"] as JsonArray ?? new JsonArray()).Any(w => w is not null && (w.ToString().Contains("Video", StringComparison.OrdinalIgnoreCase) || w.ToString().Contains("chưa tải", StringComparison.OrdinalIgnoreCase) || w.ToString().Contains("không tải", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Media nguồn chưa đầy đủ hoặc có video chưa phân tích. Cần xem thủ công.");
        var images = new List<string>();
        foreach(var media in article["media"] as JsonArray ?? new JsonArray())
        {
            var path=BrandPolicy.LocalMedia(environment.ContentRootPath,media?["url"]?.ToString());
            if(media?["type"]?.ToString()!="image" || path is null || !new[]{".jpg",".jpeg",".png",".webp"}.Contains(Path.GetExtension(path).ToLowerInvariant()) || new FileInfo(path).Length>5000000 || images.Count>=4)
                throw new InvalidOperationException("Chưa đọc được đủ ảnh gốc: tối đa 4 ảnh JPEG/PNG/WebP, mỗi ảnh 5 MB.");
            images.Add(Convert.ToBase64String(await File.ReadAllBytesAsync(path)));
        }
        if(AiImages.Validate(images) is string error)throw new InvalidOperationException(error);
        return images;
    }
    public async Task<ServiceResult> ReadVisualContext(Guid id)
    {
        var doc=await db.FindDocumentAsync(id,"articles");
        if(doc is null)return ServiceResult.NotFound();
        var article=JsonNode.Parse(doc.Json)!;
        List<string> images;
        try { images=await ReadSourceImages(article); }
        catch(InvalidOperationException ex){return ServiceResult.Conflict(new{error=ex.Message});}
        if(images.Count==0)return ServiceResult.BadRequest(new{error="Bài nguồn không có ảnh."});
        if(!ai.TryAcquire())return ServiceResult.Conflict(new{error="AI đang bận."});
        try
        {
            var context=await ai.Rewrite(new RewriteRequest("Đọc chữ trong ảnh, không diễn giải.", Mode:"read-image", Temperature:0, Images:images));
            return ServiceResult.Ok(new{context,imageCount=images.Count,imageFingerprint=VisualFingerprint(article["content"]?.ToString()??"",images)});
        }
        catch(AiModelUnavailableException ex){return ServiceResult.Json(new{error=ex.Message,code="AI_MODEL_UNAVAILABLE"},statusCode:503);}
        catch(AiGatewayException ex){return ServiceResult.Json(new{error=ex.Message,code=ex.Code},statusCode:ex.Status);}
        catch(Exception ex){logger.LogError(ex,"Image transcription failed");return ServiceResult.Json(new{error="AI chưa hoàn tất đọc ảnh. Kiểm tra kết nối/model trên máy AI rồi thử lại; chưa tạo bản rewrite."},statusCode:502);}
        finally{ai.Release();}
    }
    public Task<ServiceResult> CreateDraft(Guid id,JsonObject request)=>CreateDraftCore(id,request,true);
    async Task<ServiceResult> CreateDraftCore(Guid id, JsonObject request,bool persist)
    {
        var doc = await db.FindDocumentAsync(id, "articles");
        if (doc is null) return ServiceResult.NotFound(new { error = "Không tìm thấy bài gốc." });
        var article = JsonNode.Parse(doc.Json)!;
        if (article["deletedAt"] is not null) return ServiceResult.Conflict(new { error = "Bài đã bị loại khỏi thư viện, không tạo bản nháp." });
        var branch = request["branch"]?.ToString();
        if (branch is not ("repost" or "rewrite")) return ServiceResult.BadRequest(new { error = "Chọn repost hoặc rewrite." });
        var source = article["content"]?.ToString();
        if (string.IsNullOrWhiteSpace(source) || source.Length > 20000) return ServiceResult.BadRequest(new { error = "Bài gốc cần từ 1 đến 20.000 ký tự." });
        Document? brand = null;
        if (Guid.TryParse(article["brandId"]?.ToString(), out var brandId)) brand = await db.FindDocumentAsync(brandId, "brands");
        var profile = brand is null ? null : JsonNode.Parse(brand.Json);
        if (profile is null || profile["deletedAt"] is not null) return ServiceResult.BadRequest(new { error = "Thương hiệu không còn hoạt động." });
        var channels = BrandPolicy.Channels(profile).Where(c => c is not null && BrandPolicy.Flag(c, "active")).ToList();
        var channelId = request["channelId"]?.ToString();
        var channel = channels.FirstOrDefault(c => c?["id"]?.ToString() == channelId) ?? (string.IsNullOrEmpty(channelId) && channels.Count == 1 ? channels[0] : null);
        if (channel is null) return ServiceResult.BadRequest(new { error = "Chọn một kênh đăng đang bật của thương hiệu." });
        var content = source;
        var rewriteImages = new List<string>();
        if (branch == "rewrite")
        {
            try { rewriteImages = await ReadSourceImages(article); }
            catch (InvalidOperationException ex) { return ServiceResult.Conflict(new { error = ex.Message }); }
            await ai.AcquireAsync();
            try
            {
                var instructions = BrandPolicy.Instructions(profile, channel) + "\n" + RewritePrompts.ImageContext;
                content = await ai.Rewrite(new RewriteRequest(source, channel["tone"]?.ToString() ?? profile["tone"]?.ToString() ?? "Thân thiện", "rewrite", instructions, Temperature: .55, Images: rewriteImages));
                content = RewriteOutput.Validate(source, content);
            }
            catch (UnchangedRewriteException ex) { return ServiceResult.Json(new { error = ex.Message }, statusCode: 422); }
            catch (AiModelUnavailableException ex) { return ServiceResult.Json(new { error = ex.Message, code = "AI_MODEL_UNAVAILABLE" }, statusCode: 503); }
            catch (AiGatewayException ex) { return ServiceResult.Json(new { error=ex.Message, code=ex.Code }, statusCode:ex.Status); }
            catch (Exception ex) { logger.LogError(ex, "Article rewrite failed"); return ServiceResult.Json(new { error = "Bonsai chưa hoàn thành bài viết. Kiểm tra Ollama." }, statusCode: 502); }
            finally { ai.Release(); }
        }
        var draftId = Guid.NewGuid();
        var draft = new JsonObject {
            ["id"] = draftId.ToString(), ["articleId"] = id.ToString(), ["brandId"] = article["brandId"]?.ToString(),
            ["rewriteInputMode"] = branch == "rewrite" ? "caption-with-image-context" : null,
            ["rewriteImagesProvided"] = rewriteImages.Count, ["rewriteVisionModel"] = branch == "rewrite" && rewriteImages.Count > 0 ? "bonsai-2:27b" : null,
            ["channelId"] = channel["id"]?.ToString(), ["channelSnapshot"] = channel.DeepClone(), ["brandPolicySnapshot"] = profile.DeepClone(),
            ["publishAccountId"] = channel["publishAccountId"]?.ToString(), ["branch"] = branch, ["content"] = content, ["sourceContent"] = source, ["sourceUrl"] = article["sourceUrl"]?.ToString(),
            ["media"] = article["media"]?.DeepClone() ?? new JsonArray(), ["status"] = "awaiting_review", ["review"] = new JsonObject(),
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("O")
        };
        if(!persist)return ServiceResult.Ok(draft);
        db.AddDocument(new Document { Id = draftId, Kind = "drafts", Json = draft.ToJsonString() });
        db.AddLog(new Audit { Message = $"Đã tạo bản nháp {branch} {draftId} từ bài {id}, kênh {channel["name"]}; chờ duyệt." });
        await db.SaveChangesAsync();
        return ServiceResult.Ok(draft);
    }

    static string? ApplyDraftEdits(JsonObject draft, JsonObject request)
    {
        if (request["content"] is not JsonValue contentValue || !contentValue.TryGetValue<string>(out var content) || string.IsNullOrWhiteSpace(content) || content.Length > 20000) return "Nội dung cần từ 1 đến 20.000 ký tự.";
        if (request["review"] is not JsonObject review || review.Any(pair => pair.Value is not JsonValue value || !value.TryGetValue<bool>(out _))) return "Checklist duyệt phải là các lựa chọn bật/tắt.";
        var planned = request["plannedAt"]?.ToString();
        if (!string.IsNullOrWhiteSpace(planned))
        {
            if ((!planned.EndsWith('Z') && !System.Text.RegularExpressions.Regex.IsMatch(planned, @"[+-]\d{2}:\d{2}$")) || !DateTimeOffset.TryParse(planned, out var instant) || instant <= DateTimeOffset.UtcNow) return "Giờ dự kiến phải ở tương lai, có múi giờ.";
            draft["plannedAt"] = instant.ToUniversalTime().ToString("O");
        }
        else draft["plannedAt"] = null;
        draft["content"] = content;
        draft["review"] = review.DeepClone();
        draft["publishAccountId"] = request["publishAccountId"]?.ToString();
        return null;
    }

    public async Task<ServiceResult> SaveDraft(Guid id, JsonObject request)
    {
        var doc = await db.FindDocumentAsync(id, "drafts");
        if (doc is null) return ServiceResult.NotFound();
        var draft = JsonNode.Parse(doc.Json)!.AsObject();
        if (draft["status"]?.ToString() != "awaiting_review") return ServiceResult.Conflict(new { error = "Không sửa bản nháp đã duyệt." });
        if (ApplyDraftEdits(draft, request) is string error) return ServiceResult.BadRequest(new { error });
        draft["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        return await db.TryUpdateDraftAsync(id,doc.Json,draft.ToJsonString(),$"Đã lưu bản nháp {id}")?ServiceResult.Ok(draft):ServiceResult.Conflict(new{error="Bản nháp vừa thay đổi. Tải lại trước khi lưu."});
    }

    public async Task<ServiceResult> ApproveDraft(Guid id, JsonObject request, string reviewer)
    {
        var doc = await db.FindDocumentAsync(id, "drafts"); if (doc is null) return ServiceResult.NotFound();
        var draft = JsonNode.Parse(doc.Json)!.AsObject();
        if (draft["status"]?.ToString() != "awaiting_review") return ServiceResult.Conflict(new { error = "Bản nháp đã được duyệt; không gửi lại." });
        if (ApplyDraftEdits(draft, request) is string error) return ServiceResult.BadRequest(new { error });
        Document? brand = null; if (Guid.TryParse(draft["brandId"]?.ToString(), out var brandId)) brand = await db.FindDocumentAsync(brandId, "brands");
        var profile = brand is null ? null : JsonNode.Parse(brand.Json);
        if (profile is null || profile["deletedAt"] is not null) return ServiceResult.BadRequest(new { error = "Thương hiệu không còn hoạt động." });
        var channel = BrandPolicy.SelectChannel(profile, draft["channelId"]?.ToString() ?? "legacy-group");
        if (channel is null) return ServiceResult.Conflict(new { error = "Kênh đích đã ngưng hoặc bị xóa. Tạo lại bản nháp với kênh đang hoạt động." });
        if (draft["channelSnapshot"]?["url"] is JsonNode savedUrl && savedUrl.ToString() != channel["url"]?.ToString()) return ServiceResult.Conflict(new { error = "URL kênh đã thay đổi. Tạo lại bản nháp để xác nhận đúng nơi đăng." });
        var count = BrandPolicy.WordCount(draft["content"]!.ToString());
        if (count < BrandPolicy.Number(channel, "minWords", 80) || count > BrandPolicy.Number(channel, "maxWords", 250)) return ServiceResult.BadRequest(new { error = $"Nội dung đang có {count} từ; kênh yêu cầu {channel["minWords"]}–{channel["maxWords"]} từ." });
        var review = draft["review"]!;
        var required = new List<(string Key, string Label)> { ("topicReviewed", "Đúng chủ đề và tone thương hiệu") };
        if (BrandPolicy.Flag(profile, "verifyInformation")) required.Add(("sourceVerified", "Đã kiểm chứng nguồn, số liệu và thời hạn"));
        if (BrandPolicy.Flag(profile, "requireMediaRights")) required.Add(("mediaRightsConfirmed", "Có quyền sử dụng ảnh/video và nội dung gốc"));
        if (BrandPolicy.Flag(profile, "requireSponsorLabel")) required.Add(("sponsorshipReviewed", "Đã kiểm tra và gắn nhãn tài trợ nếu có"));
        if (BrandPolicy.Flag(channel, "requireBranding", channel["type"]?.ToString() == "page")) required.Add(("brandingConfirmed", "Media đáp ứng nhận diện thương hiệu"));
        var missing = required.Where(item => !BrandPolicy.Flag(review, item.Key, false)).Select(item => item.Label).ToArray();
        if (missing.Length > 0) return ServiceResult.BadRequest(new { error = "Chưa hoàn tất checklist duyệt.", missing });
        var media = draft["media"] as JsonArray;
        if (media is null || media.Count == 0) return ServiceResult.Conflict(new { error = "Thiếu ảnh/video gốc. Chưa thể duyệt đăng." });
        foreach (var item in media)
            if (item?["status"]?.ToString() != "downloaded" || BrandPolicy.LocalMedia(environment.ContentRootPath, item?["url"]?.ToString()) is null) return ServiceResult.Conflict(new { error = "Ảnh/video gốc chưa tải đầy đủ hoặc file không tồn tại." });
        if (!Guid.TryParse(draft["publishAccountId"]?.ToString(), out var publishId)) return ServiceResult.BadRequest(new { error = "Chọn tài khoản đăng." });
        var publisher = await db.FindDocumentAsync(publishId, "accounts"); var account = publisher is null ? null : JsonNode.Parse(publisher.Json);
        if (publisher?.ProtectedSecret is null || account?["role"]?.ToString() != "publish" || account?["enabled"]?.GetValue<bool>() == false || account?["facebookStatus"]?.ToString() != "active") return ServiceResult.BadRequest(new { error = "Tài khoản đăng cần đúng vai trò, đang bật, có phiên và đã kiểm tra Facebook hoạt động." });
        draft["channelId"] = channel["id"]?.ToString(); draft["channelSnapshot"] = channel.DeepClone(); draft["brandPolicySnapshot"] = profile.DeepClone();
        draft["destinationUrl"] = channel["url"]?.ToString(); draft["facebookGroupUrl"] = channel["type"]?.ToString() == "group" ? channel["url"]?.ToString() : null;
        draft["status"] = "approved_pending_worker"; draft["approvedAt"] = DateTimeOffset.UtcNow.ToString("O"); draft["reviewedBy"] = reviewer;
        draft["updatedAt"]=DateTimeOffset.UtcNow.ToString("O");
        return await db.TryUpdateDraftAsync(id,doc.Json,draft.ToJsonString(),$"Đã duyệt {id}; chưa đăng, chờ worker.")?ServiceResult.Ok(draft):ServiceResult.Conflict(new{error="Bản nháp vừa thay đổi. Tải lại trước khi duyệt."});
    }

    static string? ReadPublication(JsonObject request,out string? url,out DateTimeOffset at)
    {
        at=default;
        url=request["publishedUrl"]?.ToString()?.Trim();
        if(url?.Length>2048)return "Link ghi chú tối đa 2.048 ký tự.";
        if(string.IsNullOrWhiteSpace(url))url=null;
        var text=request["publishedAt"]?.ToString();
        if(string.IsNullOrWhiteSpace(text)||!System.Text.RegularExpressions.Regex.IsMatch(text,@"(?:Z|[+-]\d{2}:\d{2})$")||!DateTimeOffset.TryParse(text,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out at)||at>DateTimeOffset.UtcNow)return "Thời điểm đã đăng phải hợp lệ, có múi giờ và không ở tương lai.";
        if(request["confirmed"] is not JsonValue flag||!flag.TryGetValue<bool>(out var confirmed)||!confirmed)return "Xác nhận bạn đã đăng bài thủ công trước khi ghi nhận.";
        return null;
    }
    static void SetPublication(JsonObject draft,string? url,DateTimeOffset at,string actor)
    {
        draft["status"]="published";draft["publishedUrl"]=url;draft["publishedAt"]=at.ToUniversalTime().ToString("O");
        draft["publicationMethod"]="manual";draft["publicationEvidence"]="user_reported";draft["publishedBy"]=actor;draft["publicationRecordedAt"]=DateTimeOffset.UtcNow.ToString("O");draft["updatedAt"]=DateTimeOffset.UtcNow.ToString("O");
    }
    public async Task<ServiceResult> MarkArticlePublished(Guid id,JsonObject request,string actor)
    {
        if(ReadPublication(request,out var url,out var at) is string error)return ServiceResult.BadRequest(new{error});
        var built=await CreateDraftCore(id,new JsonObject{["branch"]="repost",["channelId"]=request["channelId"]?.DeepClone()},false);
        if(built.Status!=200||built.Value is not JsonObject record)return built;
        var key=id+"|"+record["channelId"]+"|"+(string.IsNullOrEmpty(url)?"manual-no-link":ArticleDeduplication.SourceKey(url)??url.TrimEnd('/'));
        var recordId=new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))[..16]);
        record["id"]=recordId.ToString();SetPublication(record,url!,at,actor);
        var saved=await db.SaveManualPublicationAsync(new Document{Id=recordId,Kind="drafts",Json=record.ToJsonString()},$"Ghi nhận đăng thủ công trực tiếp bài nguồn {id}");
        var value=JsonNode.Parse(saved.Json)!.AsObject();
        return DateTimeOffset.TryParse(value["publishedAt"]?.ToString(),out var savedAt)&&savedAt==at?ServiceResult.Ok(value):ServiceResult.Conflict(new{error="Bài này đã được ghi nhận cho kênh và link đó với thời điểm khác. Mở mục Đã đăng để kiểm tra."});
    }

    public async Task<ServiceResult> MarkPublished(Guid id,JsonObject request,string actor)
    {
        var doc=await db.FindDocumentAsync(id,"drafts");if(doc is null)return ServiceResult.NotFound();
        var draft=JsonNode.Parse(doc.Json)!.AsObject();
        if(ReadPublication(request,out var url,out var at) is string error)return ServiceResult.BadRequest(new{error});
        if(draft["status"]?.ToString()=="published")
            return draft["publishedUrl"]?.ToString()==url&&DateTimeOffset.TryParse(draft["publishedAt"]?.ToString(),out var saved)&&saved==at?ServiceResult.Ok(draft):ServiceResult.Conflict(new{error="Bản nháp đã được ghi nhận đăng. Không ghi đè thông tin cũ."});
        if(draft["deletedAt"] is not null||draft["status"]?.ToString() is not ("awaiting_review" or "approved_pending_worker"))return ServiceResult.Conflict(new{error="Trạng thái bản nháp không cho phép ghi nhận đã đăng."});
        if(request["expectedVersion"] is not null&&request["expectedVersion"]?.ToString()!=(draft["updatedAt"]??draft["createdAt"])?.ToString())return ServiceResult.Conflict(new{error="Nội dung vừa được sửa ở nơi khác. Tải lại trước khi đánh dấu."});
        SetPublication(draft,url!,at,actor);
        return await db.TryUpdateDraftAsync(id,doc.Json,draft.ToJsonString(),$"Người dùng ghi nhận đã đăng thủ công bản nháp {id}; chưa kiểm chứng với Facebook.")?ServiceResult.Ok(draft):ServiceResult.Conflict(new{error="Bản nháp vừa thay đổi. Tải lại trước khi đánh dấu."});
    }

    public async Task<ServiceResult> EvaluateArticle(Guid id)
    {
        var doc = await db.FindDocumentAsync(id, "articles"); if (doc is null) return ServiceResult.NotFound();
        var article = JsonNode.Parse(doc.Json)!.AsObject();
        if (!Guid.TryParse(article["brandId"]?.ToString(), out var brandId)) return ServiceResult.BadRequest(new { error = "Bài chưa gắn thương hiệu." });
        var brand = await db.FindDocumentAsync(brandId, "brands"); var profile = brand is null ? null : JsonNode.Parse(brand.Json);
        if (profile is null || profile["deletedAt"] is not null) return ServiceResult.BadRequest(new { error = "Thương hiệu không còn hoạt động." });
        var source = article["content"]?.ToString();
        if (string.IsNullOrWhiteSpace(source) || source.Length > 20000) return ServiceResult.BadRequest(new { error = "Nội dung cần từ 1 đến 20.000 ký tự." });
        await ai.AcquireAsync();
        try
        {
            var mediaIncomplete=false;
            var images = new List<string>(); var warnings = (article["mediaWarnings"] as JsonArray ?? new JsonArray()).Select(n => n!.ToString()).ToList();
            foreach (var media in article["media"] as JsonArray ?? new JsonArray())
            {
                if (media?["type"]?.ToString() == "video") { mediaIncomplete=true; warnings.Add("Video chưa được phân tích khung hình; cần xem thủ công."); continue; }
                var path = BrandPolicy.LocalMedia(environment.ContentRootPath, media?["url"]?.ToString());
                if (path is null || !new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(path).ToLowerInvariant()) || new FileInfo(path).Length > 5_000_000 || images.Count >= 4) { mediaIncomplete=true; warnings.Add("Có media chưa phân tích được; cần xem thủ công."); continue; }
                images.Add(Convert.ToBase64String(await File.ReadAllBytesAsync(path)));
            }
            var evaluationProfile=profile.DeepClone();
            if(profile["learnedScope"] is JsonObject learned)
            {
                static string Short(JsonNode? n,int max){var value=n?.ToString()??"";return value[..Math.Min(max,value.Length)];}
                evaluationProfile["rules"]=(profile["rules"]?.ToString()??"")+"\nScope Hermes tham chiếu (không lấn át yêu cầu hiện tại/nội dung cấm): "+Short(learned["summary"],700)+" "+Short(learned["suggestedTopics"],1000);
            }
            if(!string.IsNullOrWhiteSpace(article["crawlTopicDescription"]?.ToString()))evaluationProfile["rules"]=(profile["rules"]?.ToString()??"")+"\nChủ đề yêu cầu của lượt thu thập: "+article["crawlTopicDescription"]!.ToString();
            mediaIncomplete |= warnings.Any(w=>w.Contains("chưa tải",StringComparison.OrdinalIgnoreCase)||w.Contains("không tải",StringComparison.OrdinalIgnoreCase)||w.Contains("chưa đọc được link",StringComparison.OrdinalIgnoreCase));
            var result = ClefSelectionPolicy.Apply(await ai.Evaluate(source, evaluationProfile, images),evaluationProfile);
            var resultWarnings = result["warnings"]!.AsArray().Select(node => node!.ToString()).Concat(warnings).Distinct().ToArray();
            article["aiStatus"] = mediaIncomplete && result["status"]!.ToString() == "recommended" ? "review" : result["status"]!.ToString();
            article["aiSourceLanguage"]=result["sourceLanguage"]?.DeepClone();article["aiTopicRelationship"]=result["topicRelationship"]?.DeepClone();article["aiSelectionVersion"]=result["selectionVersion"]?.DeepClone();article["aiModelStatus"]=result["modelStatus"]?.DeepClone();article["aiSelectionNotes"]=result["selectionNotes"]?.DeepClone();
            article["aiDecision"] = result["decision"]?.DeepClone(); article["aiScore"] = result["score"]!.DeepClone(); article["aiReason"] = result["reason"]!.ToString(); article["aiTopic"] = result["topic"]!.ToString();
            article["aiWarnings"] = new JsonArray(resultWarnings.Select(text => JsonValue.Create(text)).ToArray());
            article["aiEvaluatedAt"] = DateTimeOffset.UtcNow.ToString("O"); article["aiBrandUpdatedAt"] = profile["updatedAt"]?.ToString(); article["aiImagesAnalyzed"] = images.Count;
            var activeChannels = BrandPolicy.Channels(profile).Where(c => c is not null && BrandPolicy.Flag(c, "active")).ToList();
            if (article["channelEvaluationRequired"]?.GetValue<bool>() == true || activeChannels.Count > 0)
            {
                article["channelEvaluationRequired"]=true;
                var matches = new JsonArray();
                if (result["status"]?.ToString() != "rejected")
                {
                    if (activeChannels.Count == 0) return ServiceResult.Conflict(new { error="Không còn kênh đang bật để đối chiếu bài." });
                    foreach (var channel in activeChannels)
                    {
                        var channelProfile = evaluationProfile.DeepClone();
                        if(!string.IsNullOrWhiteSpace(channel!["allowedTopics"]?.ToString()))channelProfile["allowedTopics"]=channel["allowedTopics"]!.DeepClone();
                        channelProfile["channelName"] = channel!["name"]?.ToString();
                        channelProfile["channelGuidelines"] = channel["contentGuidelines"]?.ToString();
                        foreach (var field in new[] { "tone", "audience" }) if (!string.IsNullOrWhiteSpace(channel[field]?.ToString())) channelProfile[field] = channel[field]!.DeepClone();
                        // Channel guidelines already have their own prompt field; do not duplicate them in rules.
                        var match = ClefSelectionPolicy.Apply(await ai.Evaluate(source, channelProfile, images),channelProfile);
                        matches.Add(new JsonObject { ["channelId"]=channel["id"]?.ToString(), ["name"]=channel["name"]?.ToString(), ["status"]=match["status"]?.ToString(), ["score"]=match["score"]?.DeepClone(), ["reason"]=match["reason"]?.ToString(), ["sourceLanguage"]=match["sourceLanguage"]?.DeepClone(), ["topicRelationship"]=match["topicRelationship"]?.DeepClone(), ["modelStatus"]=match["modelStatus"]?.DeepClone(), ["selectionNotes"]=match["selectionNotes"]?.DeepClone(), ["warnings"]=match["warnings"]?.DeepClone() });
                    }
                }
                SourceGroupPolicy.ApplyChannelVerdict(article, matches);
                article["aiChannelMatches"] = matches;
                article["aiChannelEvaluationStatus"] = "complete";
            }
            CrawlEvaluationState.ExcludeRejected(article);
            doc.Json = article.ToJsonString();
            await RefreshCrawlEvaluation(article);
            db.AddLog(new Audit { Message = $"Clef đánh giá bài {id}: {article["aiStatus"]}, {article["aiScore"]}/100." }); await db.SaveChangesAsync();
            return ServiceResult.Ok(article);
        }
        catch (AiGatewayException ex) { logger.LogWarning("Clef evaluation failed: {Code} for article {ArticleId}", ex.Code, id); return ServiceResult.Json(new { error = ex.Message, code = ex.Code }, statusCode: ex.Status); }
        catch (Exception ex) { logger.LogError(ex, "Clef evaluation failed"); return ServiceResult.Json(new { error = "Clef chưa đánh giá được. Kiểm tra Ollama/model; kết quả cũ được giữ nguyên." }, statusCode: 502); }
        finally { ai.Release(); }
    }

    async Task RefreshCrawlEvaluation(JsonObject article)
    {
        if (!Guid.TryParse(article["crawlTaskId"]?.ToString(), out var taskId)) return;
        var taskDoc = await db.FindDocumentAsync(taskId, "tasks");
        if (taskDoc is null) return;
        var task = JsonNode.Parse(taskDoc.Json)!.AsObject();
        if (task["workerNode"]?.ToString() != "Facebook local") return;
        if (task["status"]?.ToString() is "running" or "pending" || task["errorCode"]?.ToString() == "USER_PAUSED") return;
        var evaluation = new CrawlEvaluationState();
        foreach (var row in await db.ListDocumentsAsync("articles"))
        {
            var item = JsonNode.Parse(row.Json)!.AsObject();
            if (item["crawlTaskId"]?.ToString() == taskId.ToString()) evaluation.Record(ServiceResult.Ok(item));
        }
        evaluation.WriteTo(task);
        var captured = task["crawledPosts"]?.GetValue<int>() ?? 0;
        var target = task["maxPosts"]?.GetValue<int>() ?? 1;
        if (task["status"]?.ToString() == "paused" && task["errorCode"]?.ToString() == "CLEF_EVALUATION_INCOMPLETE"
            && evaluation.Evaluated >= captured)
        {
            var complete = captured >= target;
            task["status"] = complete ? "completed" : "paused";
            task["errorCode"] = complete ? null : "COLLECTION_INCOMPLETE";
            task["errorReason"] = complete
                ? "Đã thu thập đủ bài và hoàn tất đánh giá Clef sau khi đánh giá lại. Không đăng Facebook."
                : $"Clef đã đánh giá {evaluation.Evaluated}/{captured} bài đã thu thập; còn thiếu {target-captured} bài thu thập. Tạo lượt cào mới để tiếp tục; không tự chạy lại Facebook.";
        }
        task["progress"] = CrawlEvaluationState.Progress(task["status"]?.ToString() ?? "paused", captured, target, evaluation.Evaluated);
        await db.TryUpdateTaskAsync(taskId, taskDoc.Json, task.ToJsonString(), $"Cập nhật thống kê Clef tác vụ {taskId}");
    }

    public async Task<ServiceResult> Rewrite(RewriteRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Text) || r.Text.Length > 20000 || r.Instructions.Length > 10000) return ServiceResult.BadRequest(new { error = "Nội dung cần từ 1 đến 20.000 ký tự; chỉ dẫn tối đa 10.000 ký tự." });
        if (AiImages.Validate(r.Images) is string imageError) return ServiceResult.BadRequest(new { error = imageError });
        await ai.AcquireAsync();
        try
        {
            var text = await ai.Rewrite(r);
            var id = Guid.NewGuid(); var result = new { id, model = "bonsai-2:27b", source = r.Text, text, r.Tone, r.Mode, createdAt = DateTimeOffset.UtcNow };
            db.AddDocument(new Document { Id = id, Kind = "rewrites", Json = JsonSerializer.Serialize(result) });
            db.AddLog(new Audit { Message = $"Bonsai hoàn thành rewrite {id}" }); await db.SaveChangesAsync(); return ServiceResult.Ok(result);
        }
        catch (UnchangedRewriteException ex) { return ServiceResult.Json(new { error = ex.Message }, statusCode: 422); }
        catch (Exception ex) { logger.LogError(ex, "Ollama failed"); db.AddLog(new Audit { Message = "Rewrite thất bại: kiểm tra Ollama hoặc model." }); await db.SaveChangesAsync(); return ServiceResult.Json(new { error = "Ollama không hoàn thành tác vụ. Xem log máy chủ." }, statusCode: 502); }
        finally { ai.Release(); }
    }
}
