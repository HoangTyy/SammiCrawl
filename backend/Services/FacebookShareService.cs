using System.Text.Json.Nodes;

namespace SammiCrawl.Backend.Services;

// Native Facebook share, not a copied/re-written post. No AI or local media required.
public sealed class FacebookShareService(IProjectRepository db, CrawlWorkerIdentity identity)
{
    public async Task<ServiceResult> Submit(Guid articleId, JsonObject request, string actor)
    {
        if(!BrandPolicy.Flag(request,"confirmed",false))return ServiceResult.BadRequest(new{error="Xác nhận chia sẻ thật bài gốc vào nhóm đã chọn."});
        var source=await db.FindDocumentAsync(articleId,"articles");
        var article=source is null?null:JsonNode.Parse(source.Json);
        if(article is null||article["deletedAt"] is not null)return ServiceResult.NotFound();
        var sourceUrl=article["sourceUrl"]?.ToString();
        if(ArticleDeduplication.SourceKey(sourceUrl??"") is null||!BrandPolicy.FacebookPostUrl(sourceUrl)||!Uri.TryCreate(sourceUrl,UriKind.Absolute,out var sourceUri)||sourceUri.Port!=443||sourceUri.UserInfo.Length>0)return ServiceResult.BadRequest(new{error="Bài nguồn cần link bài Facebook hợp lệ để chia sẻ nguyên bản."});
        if(!Guid.TryParse(article["brandId"]?.ToString(),out var brandId))return ServiceResult.BadRequest(new{error="Bài chưa có thương hiệu."});
        var brand=await db.FindDocumentAsync(brandId,"brands");var profile=brand is null?null:JsonNode.Parse(brand.Json);
        var channel=profile is null||profile["deletedAt"] is not null?null:BrandPolicy.SelectChannel(profile,request["channelId"]?.ToString()??"");
        if(channel is null||channel["type"]?.ToString()!="group"||SourceGroupPolicy.CanonicalUrl(channel["url"]?.ToString()??"") is null)
            return ServiceResult.BadRequest(new{error="Chọn kênh Group đang hoạt động của thương hiệu."});
        if(!Guid.TryParse(request["publishAccountId"]?.ToString(),out var accountId))return ServiceResult.BadRequest(new{error="Chọn tài khoản đăng để chia sẻ."});
        var account=await db.FindDocumentAsync(accountId,"accounts");var a=account is null?null:JsonNode.Parse(account.Json);
        if(!Eligible(account,a))return ServiceResult.BadRequest(new{error="Tài khoản cần vai trò đăng, đang hoạt động và có phiên Facebook."});
        var key="native-share|"+ArticleDeduplication.SourceKey(sourceUrl!)+"|"+SourceGroupPolicy.CanonicalUrl(channel["url"]!.ToString());
        var id=new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))[..16]);
        var record=new JsonObject{["id"]=id.ToString(),["articleId"]=articleId.ToString(),["brandId"]=brandId.ToString(),["channelId"]=channel["id"]?.ToString(),["channelSnapshot"]=channel.DeepClone(),
            ["sourceUrl"]=sourceUrl,["sourceAuthor"]=article["sourceAuthor"]?.ToString(),["sourceContent"]=article["content"]?.ToString(),["content"]=article["content"]?.ToString()??"",
            ["media"]=new JsonArray(),["branch"]="share",["publicationMethod"]="facebook_native_share",["status"]="share_queued",["publishAccountId"]=accountId.ToString(),
            ["workerOwnerId"]=identity.Id,["requestedBy"]=actor,["createdAt"]=DateTimeOffset.UtcNow.ToString("O")};
        var saved=await db.SaveManualPublicationAsync(new Document{Id=id,Kind="drafts",Json=record.ToJsonString()},$"Xếp hàng chia sẻ bài gốc {articleId} vào Group; giữ người đăng gốc.");
        return ServiceResult.Ok(JsonNode.Parse(saved.Json));
    }
    public static bool Eligible(Document? account,JsonNode? a)=>account?.ProtectedSecret is not null&&a?["deletedAt"] is null&&a?["role"]?.ToString()=="publish"&&BrandPolicy.Flag(a,"enabled")&&a["facebookStatus"]?.ToString()=="active";
    public async Task<ServiceResult> Cancel(Guid id)
    {
        var doc=await db.FindDocumentAsync(id,"drafts");if(doc is null)return ServiceResult.NotFound();
        var value=JsonNode.Parse(doc.Json)!.AsObject();
        if(value["status"]?.ToString()!="share_queued")return ServiceResult.Conflict(new{error="Chỉ hủy lượt chia sẻ còn đang chờ."});
        value["status"]="share_cancelled";
        return await db.TryUpdateDraftAsync(id,doc.Json,value.ToJsonString(),$"Hủy chia sẻ đang chờ {id}")?ServiceResult.Ok(value):ServiceResult.Conflict(new{error="Lượt chia sẻ vừa đổi trạng thái."});
    }
}
