using System.Text.Json.Nodes;
using System.Globalization;
namespace SammiCrawl.Backend.Services;

public static class ClefSelectionPolicy
{
    public const int DefaultMinimumScore=75;
    public static int Minimum(JsonNode profile)=>BrandPolicy.Number(profile,"selectionMinScore",DefaultMinimumScore);
    public static string TargetLanguage(JsonNode profile)
    {
        var value=(profile["language"]?.ToString()??"vi").Trim().ToLowerInvariant().Replace(" ","");
        return value is "" or "vi" or "vi-vn" or "vietnamese" or "tiếngviệt" or "tiengviet"?"vi":value is "en" or "en-us" or "en-gb" or "english" or "tiếnganh" or "tienganh"?"en":"unknown";
    }
    // Only downgrade inconsistent recommendations. Never promote a rejected/review result to recommended.
    public static JsonObject Apply(JsonObject input,JsonNode profile)
    {
        var result=input.DeepClone().AsObject();
        var state=result["status"]?.ToString();
        if(state is not ("recommended" or "review" or "rejected")||!double.TryParse(result["score"]?.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var score)||!double.IsFinite(score)||score<0||score>100)throw AiGatewayException.InvalidOutput();
        if(result["selectionVersion"]?.ToString()=="3")return result;
        var decision=result["decision"];var reason=decision?["reason"]?["choice"]?.ToString();var topic=decision?["topic"]?["choice"]?.ToString();
        var language=decision?["language"]?["choice"]?.ToString()??"unknown";
        var relation=decision?["topic_relation"]?["choice"]?.ToString()??"uncertain";
        var notes=new List<string>();
        if(state=="rejected"&&reason is "unverified" or "media_unclear" or "unclear")
        {state="review";notes.Add("Thiếu xác minh hoặc media chưa rõ cần người xem lại, không tự loại bài.");}
        else if(state=="rejected"&&reason=="on_topic")
        {state="review";notes.Add("Nhãn loại bài mâu thuẫn với lý do đúng chủ đề; cần xem lại.");}
        if(state=="rejected"&&reason=="off_topic"&&score>=Minimum(profile))
        {state="review";notes.Add("Điểm phù hợp cao mâu thuẫn với lý do ngoài chủ đề; cần người xem lại.");}
        if(state=="recommended")
        {
            if(language is "unknown" or "mixed" or "other" || TargetLanguage(profile)=="unknown" || language!=TargetLanguage(profile))notes.Add(language=="en"&&TargetLanguage(profile)=="vi"?"Bài chủ yếu bằng tiếng Anh; kênh đang chọn nguồn tiếng Việt.":"Ngôn ngữ chính chưa khớp hoặc chưa xác định; cần xem lại.");
            if(relation!="direct")notes.Add("Nội dung chưa được xác nhận liên quan trực tiếp với chủ đề kênh; không đề xuất chỉ vì từ khóa trùng.");
            if(score<Minimum(profile))notes.Add($"Điểm phù hợp {score:0.#}/100 chưa đạt ngưỡng {Minimum(profile)}.");
            if(topic=="other"||reason is "off_topic" or "restricted" or "unclear" or "media_unclear" or "unverified")notes.Add("Chủ đề/lý do chưa đủ nhất quán để đề xuất.");
            var warnings=(result["warnings"] as JsonArray??new JsonArray()).Select(n=>n?.ToString()??"").ToArray();
            bool Risk(string key)=>double.TryParse(decision?[key]?["noul"]?.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&value>=.5;
            if(BrandPolicy.Flag(profile,"verifyInformation")&&(Risk("source_risk")||warnings.Any(w=>w.Contains("Clef yêu cầu kiểm chứng",StringComparison.Ordinal))))notes.Add("Cần xác minh nguồn, số liệu hoặc thời hạn trước khi sử dụng.");
            if(Risk("media_risk")||warnings.Any(w=>w.Contains("Clef yêu cầu xem lại ảnh",StringComparison.Ordinal)))notes.Add("Ảnh cần được đối chiếu thủ công.");
            if(notes.Count>0)state="review";
        }
        result["modelStatus"]=input["modelStatus"]?.DeepClone()??JsonValue.Create(input["status"]?.ToString());result["status"]=state;
        result["selectionVersion"]=3;result["sourceLanguage"]=language;result["topicRelationship"]=relation;result["selectionMinScore"]=Minimum(profile);
        result["selectionNotes"]=new JsonArray(notes.Select(n=>JsonValue.Create(n)).ToArray());
        if(notes.Count>0)result["reason"]=(input["reason"]?.ToString()??"")+" "+string.Join(" ",notes);
        return result;
    }
}
