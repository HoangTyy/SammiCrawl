import { useEffect } from 'react';

const tabs=['dashboard','crawler-studio','queue','data-vault','proxy-stealth','facebook-accounts','channels','crawl-presets','trash'];
export function readTab(){try{const value=sessionStorage.getItem('sammicrawl-tab');return tabs.includes(value??'')?value!:'dashboard';}catch{return 'dashboard';}}
export function usePagePosition(tab:string){
 useEffect(()=>{
  const key='sammicrawl-scroll:'+tab;let target=0;
  try{sessionStorage.setItem('sammicrawl-tab',tab);target=Math.max(0,Number(sessionStorage.getItem(key))||0);}catch{/* Storage can be unavailable. */}
  const previous=history.scrollRestoration;history.scrollRestoration='manual';
  let restoring=true;let frame=0;
  const save=()=>{if(!restoring)try{sessionStorage.setItem(key,String(window.scrollY));}catch{/* Ignore storage quota. */}};
  const stop=()=>{restoring=false;observer.disconnect();};
  const restore=()=>{if(!restoring)return;window.scrollTo(0,target);if(document.documentElement.scrollHeight-window.innerHeight>=target)stop();};
  const observer=new ResizeObserver(()=>{cancelAnimationFrame(frame);frame=requestAnimationFrame(restore);});
  observer.observe(document.body);frame=requestAnimationFrame(restore);
  const timeout=window.setTimeout(stop,15000);
  window.addEventListener('scroll',save,{passive:true});window.addEventListener('pagehide',save);
  window.addEventListener('wheel',stop,{passive:true});window.addEventListener('touchstart',stop,{passive:true});window.addEventListener('keydown',stop);
  return()=>{save();observer.disconnect();cancelAnimationFrame(frame);clearTimeout(timeout);history.scrollRestoration=previous;window.removeEventListener('scroll',save);window.removeEventListener('pagehide',save);window.removeEventListener('wheel',stop);window.removeEventListener('touchstart',stop);window.removeEventListener('keydown',stop);};
 },[tab]);
}
