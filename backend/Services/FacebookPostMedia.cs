namespace SammiCrawl.Backend.Services;
public static class FacebookPostMedia
{
    public const string WaitVisibleImages = """
    async () => {
      const images=Array.from(document.querySelectorAll('img')).filter(i=>{const r=i.getBoundingClientRect();return !i.complete&&r.width>=80&&r.height>=80&&r.bottom>0&&r.top<innerHeight;}).slice(0,24);
      await Promise.all(images.map(i=>new Promise(resolve=>{let done=false;const finish=()=>{if(done)return;done=true;i.removeEventListener('load',finish);i.removeEventListener('error',finish);resolve();};i.addEventListener('load',finish,{once:true});i.addEventListener('error',finish,{once:true});setTimeout(finish,1800);if(i.complete)finish();})));
    }
    """;
    public const string Script = """
    root => {
      const allowed=value=>{try{const u=new URL(value,location.href);return u.protocol==='https:'&&(!u.port||u.port==='443')&&!u.username&&!u.password&&(u.hostname.endsWith('.fbcdn.net')||u.hostname.endsWith('.facebook.com'))?u.href:null;}catch{return null;}};
      const nested=node=>!!node.closest('[data-commentid],[aria-label^="Comment by"],[aria-label^="Bình luận của"]')||(root.matches('[role="article"]')&&node.closest('[role="article"]')!==root);
      const photoLink=node=>{const a=node.closest('a[href]');if(!a)return false;try{const u=new URL(a.href,location.href);return /\/photo(?:s)?(?:\/|$)/.test(u.pathname)||u.searchParams.has('fbid')||u.searchParams.get('set')?.startsWith('gm.');}catch{return false;}};
      const profileLink=node=>{const a=node.closest('a[href]');if(!a||photoLink(node))return false;return /\/(?:user|profile\.php|people)(?:\/|\?|$)/.test(a.href);};
      const srcsets=text=>(text||'').split(',').map(v=>{const [url,size]=v.trim().split(/\s+/);return {url:allowed(url),rank:parseFloat(size)||0};}).filter(v=>v.url).sort((a,b)=>b.rank-a.rank).map(v=>v.url);
      const images=[],imageAlternatives={};let unresolvedImages=0;
      for(const img of root.querySelectorAll('img')) {
        if(nested(img)||profileLink(img)||img.closest('a[href*="/videos/"],a[href*="/reel/"]'))continue;
        const r=img.getBoundingClientRect(),w=Math.max(img.naturalWidth||0,Number(img.getAttribute('width'))||0,r.width),h=Math.max(img.naturalHeight||0,Number(img.getAttribute('height'))||0,r.height);
        const photo=photoLink(img);if(!photo&&!(Math.max(w,h)>=200&&Math.min(w,h)>=80))continue;
        if(img.hidden||getComputedStyle(img).display==='none')continue;
        const urls=[...srcsets(img.getAttribute('srcset')),...srcsets(img.getAttribute('data-srcset')),allowed(img.getAttribute('data-src')),allowed(img.currentSrc),allowed(img.getAttribute('src'))].filter(Boolean);
        const unique=Array.from(new Set(urls));if(!unique.length){if(photo)unresolvedImages++;continue;}
        if(!images.includes(unique[0])){images.push(unique[0]);imageAlternatives[unique[0]]=unique.slice(1,3);}
      }
      // SVG media is only accepted when it is explicitly attached to a photo link, never avatars/decorations.
      for(const img of root.querySelectorAll('svg image')) {
        if(nested(img)||!photoLink(img))continue;
        const src=allowed(img.getAttribute('href')||img.getAttribute('xlink:href'));
        if(src&&!images.includes(src)){images.push(src);imageAlternatives[src]=[];}else if(!src)unresolvedImages++;
      }
      if(!images.length&&Array.from(root.querySelectorAll('a[href]')).some(a=>!nested(a)&&photoLink(a)&&!a.querySelector('img,svg image')))unresolvedImages=Math.max(1,unresolvedImages);
      const videos=Array.from(root.querySelectorAll('video')).filter(v=>!nested(v)).map(v=>v.currentSrc||v.getAttribute('src')||v.querySelector('source')?.getAttribute('src')).filter(Boolean);
      return {images,imageAlternatives,unresolvedImages,videos};
    }
    """;
}
