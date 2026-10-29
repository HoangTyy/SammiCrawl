using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SammiCrawl.Backend.Services;

public static class FacebookPostMetadata
{
    // Read only the current post's rendered counters; never open reaction/comment controls.
    public const string Script = """
    (root, sourceUrl) => {
      const clean = s => (s || '').replace(/[\u200b-\u200f\ufeff]/g, '').replace(/\s+/g, ' ').trim();
      const visible = el => [...el.getClientRects()].some(r => r.width > 0 && r.height > 0) && !el.closest('[hidden],[aria-hidden="true"]');
      const boundary = root.closest('[role="article"],[data-pagelet^="FeedUnit"]');
      const messages = [...root.querySelectorAll('[data-ad-preview="message"],[data-ad-comet-preview="message"],[data-ad-rendering-role="story_message"]')];
      const own = el => visible(el) && !messages.some(m => m.contains(el)) && el.closest('[role="article"],[data-pagelet^="FeedUnit"]') === boundary;
      const token = '(\\d+(?:[.,\\s]\\d+)*\\s*(?:k|m|b|nghìn|ngàn|triệu|tỷ|tr)?)';
      const count = raw => {
        const text = clean(raw), match = text.match(/^(\d[\d.,\s]*?)\s*(k|m|b|nghìn|ngàn|triệu|tỷ|tr)?$/i);
        if (!match) return null;
        let n = match[1].replace(/\s/g, ''); const unit = (match[2] || '').toLowerCase();
        if (unit) {
          const split = Math.max(n.lastIndexOf('.'), n.lastIndexOf(','));
          if (split >= 0) n = n.slice(0, split).replace(/[.,]/g, '') + '.' + n.slice(split + 1);
        } else if (/^\d{1,3}([.,]\d{3})+$/.test(n)) n = n.replace(/[.,]/g, '');
        else if (!/^\d+$/.test(n)) return null;
        const multiplier = /^(k|nghìn|ngàn)$/.test(unit) ? 1e3 : /^(m|triệu|tr)$/.test(unit) ? 1e6 : /^(b|tỷ)$/.test(unit) ? 1e9 : 1;
        const value = Math.round(Number(n) * multiplier);
        return Number.isSafeInteger(value) && value >= 0 ? { value, text, approximate: !!unit } : null;
      };
      const result = { reactions: null, likes: null, comments: null, posted: null };
      const key = href => { try { const u = new URL(href, location.href); return u.pathname.match(/\/(?:posts|permalink|videos|reel)\/([^/]+)/)?.[1] || u.searchParams.get('story_fbid') || u.searchParams.get('set')?.match(/^gm\.(\d+)/)?.[1]; } catch { return null; } };
      const sourceKey = key(sourceUrl);
      const postKeys = new Set([...root.querySelectorAll('a[href]')].filter(own).map(a => key(a.href)).filter(Boolean));
      if (!sourceKey || [...postKeys].some(k => k !== sourceKey)) return result;
      const patterns = {
        reactions: [new RegExp('^(?:tất cả(?: cảm xúc)?|all reactions|reactions|cảm xúc)\\s*:?\\s*' + token + '$', 'i'), new RegExp('^' + token + '\\s*(?:(?:lượt|người)\\s*)?(?:cảm xúc|bày tỏ cảm xúc|reactions?|đã bày tỏ cảm xúc)$', 'i')],
        likes: [new RegExp('^' + token + '\\s*(?:lượt\\s*)?(?:thích|likes?)$', 'i'), new RegExp('^(?:thích|likes?)\\s*:\\s*' + token + '(?:\\s*(?:người|people))?$', 'i')],
        comments: [new RegExp('^(?:(?:xem tất cả|view all)\\s*)?' + token + '\\s*(?:bình luận|comments?)$', 'i'), new RegExp('^(?:bình luận|comments?)\\s*:\\s*' + token + '$', 'i')]
      };
      const save = (kind, parsed) => { if (parsed && (!result[kind] || (result[kind].approximate && !parsed.approximate))) result[kind] = parsed; };
      for (const el of root.querySelectorAll('a,button,[role="button"],span,[aria-label]')) {
        if (!own(el)) continue;
        const label = clean(el.getAttribute('aria-label')), text = clean(el.innerText);
        for (const value of new Set([label, text])) {
          if (!value || value.length > 120) continue;
          for (const [kind, expressions] of Object.entries(patterns)) {
            for (const re of expressions) { const match = value.match(re); if (match) save(kind, count(match[1])); }
          }
        }
        // Facebook often puts a plain number inside a labelled reaction-summary button.
        if (/^(?:xem ai đã bày tỏ cảm xúc|see who reacted|all reactions|tất cả cảm xúc)/i.test(label)) save('reactions', count(text));
      }
      for (const el of root.querySelectorAll('time[datetime],abbr[data-utime],[data-utime],a[href]')) {
        if (!own(el)) continue;
        const link = el.matches('a[href]') ? el : el.closest('a[href]');
        if (link && (key(link.href) !== sourceKey || /[?&](?:comment_id|reply_comment_id)=/.test(link.href))) continue;
        if (!link && !el.matches('time[datetime],[data-utime]')) continue;
        const nested = el.querySelector('time[datetime],[data-utime]');
        const unix = el.getAttribute('data-utime') || nested?.getAttribute('data-utime');
        const datetime = el.getAttribute('datetime') || nested?.getAttribute('datetime');
        const text = clean(el.getAttribute('title') || el.getAttribute('aria-label') || el.innerText);
        const timeText = /^(?:\d+\s*(?:giây|phút|giờ|ngày|tuần|tháng|năm|s|m|h|d|w|y|seconds?|minutes?|hours?|days?|weeks?|months?|years?)(?:\s*(?:trước|ago))?|vừa xong|hôm nay|hôm qua|just now|yesterday|today)$/i.test(text)
          || /\d{1,2}[/:]\d{1,2}|\d+\s+tháng\s+\d+|(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\s+\d+/i.test(text);
        if (!unix && !datetime && (!link || !timeText)) continue;
        const posted = { unix, datetime, text: text.slice(0, 250) };
        if (!result.posted || unix || datetime) result.posted = posted;
        if (unix || datetime) break;
      }
      return result;
    }
    """;

    public static void Apply(JsonObject article, JsonNode? metadata, DateTimeOffset capturedAt)
    {
        if (metadata is null) return;
        foreach (var (kind, field, prefix) in new[] { ("reactions", "reactions", "reaction"), ("likes", "likes", "like"), ("comments", "comments", "comment") })
        {
            var count = metadata[kind];
            if (count?["value"] is not JsonValue value || !value.TryGetValue<long>(out var number) || number < 0 || number > 9007199254740991) continue;
            article[field] = number;
            article[prefix + "CountText"] = Short(count["text"]?.ToString(), 80);
            article[prefix + "CountApproximate"] = count["approximate"] is JsonValue flag && flag.TryGetValue<bool>(out var approximate) && approximate;
        }
        article["interactionCapturedAt"] = capturedAt.ToUniversalTime().ToString("O");
        var posted = metadata["posted"];
        if (posted is null) return;
        article["sourcePublishedText"] = Short(posted["text"]?.ToString(), 250);
        DateTimeOffset? timestamp = null;
        if (long.TryParse(posted["unix"]?.ToString(), CultureInfo.InvariantCulture, out var unix) && unix >= 1072915200 && unix <= capturedAt.ToUnixTimeSeconds() + 300)
            timestamp = DateTimeOffset.FromUnixTimeSeconds(unix);
        var iso = posted["datetime"]?.ToString();
        if (timestamp is null && iso is not null && Regex.IsMatch(iso, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)
            && DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Year >= 2004 && date <= capturedAt.AddMinutes(5)) timestamp = date;
        if (timestamp.HasValue) article["sourcePublishedAt"] = timestamp.Value.ToUniversalTime().ToString("O");
    }

    static string? Short(string? text, int max) => string.IsNullOrWhiteSpace(text) ? null : text[..Math.Min(max, text.Length)];
}
