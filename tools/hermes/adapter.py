"""Internal Hermes CLI adapter. No browser tools, shell tools, or user credentials."""
import hmac,json,os,subprocess,threading
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
KEY=os.environ.get("HERMES_ADAPTER_KEY","")
if len(KEY)<32:raise RuntimeError("HERMES_ADAPTER_KEY must be configured")
LOCK=threading.Lock()
# Hermes supports custom-provider request overrides. Constrain generation at
# Ollama rather than repairing malformed model text or changing selected IDs.
home=os.environ.get("HERMES_HOME","/runtime")
os.makedirs(home,exist_ok=True)
with open(os.path.join(home,"config.yaml"),"w",encoding="utf-8") as config:
 json.dump({"custom_providers":[{"name":"sammicrawl-local","base_url":os.environ.get("CUSTOM_BASE_URL","http://host.docker.internal:11434/v1"),"extra_body":{"response_format":{"type":"json_object"}}}]},config)
class Handler(BaseHTTPRequestHandler):
 def log_message(self,*args):pass
 def send(self,status,data):
  if status>=400:print("Hermes adapter response",status,data.get("error",""),flush=True)
  raw=json.dumps(data,ensure_ascii=False).encode();self.send_response(status);self.send_header("Content-Type","application/json");self.send_header("Content-Length",str(len(raw)));self.end_headers();self.wfile.write(raw)
 def do_GET(self):
  if self.path=="/health":self.send(200,{"online":True,"agent":"hermes-cli","tools":False})
  else:self.send(404,{})
 def do_POST(self):
  if self.path!="/analyze":return self.send(404,{})
  if not hmac.compare_digest(self.headers.get("Authorization",""),"Bearer "+KEY):return self.send(401,{})
  size=int(self.headers.get("Content-Length","0"))
  if size<1 or size>160000:return self.send(400,{"error":"Invalid payload size"})
  if not LOCK.acquire(blocking=False):return self.send(409,{"error":"Agent busy"})
  try:
   data=json.loads(self.rfile.read(size));prompt=data.get("prompt","")
   if not isinstance(prompt,str) or not 1<=len(prompt)<=20000:return self.send(400,{"error":"Invalid prompt"})
   result=subprocess.run(["/opt/hermes/bin/hermes","chat","--query-file","-","--oneshot","-Q","--format","stream-json","--safe-mode","-t","none","--provider","custom","-m",os.environ.get("HERMES_MODEL","bonsai-2-hermes:27b"),"--max-turns","1","--run-budget","240"],input=prompt,text=True,capture_output=True,timeout=270,cwd="/tmp")
   if result.returncode:
    diagnostic=(result.stderr or result.stdout)[-1600:]
    for name,value in os.environ.items():
     if value and any(part in name for part in ("KEY","TOKEN","PASSWORD")):diagnostic=diagnostic.replace(value,"[redacted]")
    print("Hermes CLI failed:",diagnostic,flush=True)
    return self.send(502,{"error":"Hermes did not complete"})
   output=result.stdout
   if len(output)>256000:return self.send(502,{"error":"Hermes output too large"})
   # Consume only the terminal result from this invocation, never rendered text
   # deltas or a previous session's database response.
   events=[]
   for line in output.splitlines():
    try:event=json.loads(line)
    except ValueError:continue
    if isinstance(event,dict):events.append(event)
   terminal=[event for event in events if isinstance(event,dict) and event.get("type")=="result"]
   if len(terminal)!=1 or terminal[0].get("exit_code")!=0 or terminal[0].get("error"):
    return self.send(502,{"error":"Hermes result missing or failed"})
   output=terminal[0].get("text","")
   if not isinstance(output,str) or len(output)>64000:return self.send(502,{"error":"Invalid Hermes result text"})
   decoder=json.JSONDecoder();parsed=None
   for i,c in enumerate(output):
    if c!="{":continue
    try:
     obj,_=decoder.raw_decode(output[i:])
     if isinstance(obj,dict) and "summary" in obj:parsed=obj;break
    except ValueError:continue
   if parsed is None:return self.send(502,{"error":"Hermes did not return JSON"})
   self.send(200,{"text":json.dumps(parsed,ensure_ascii=False)})
  except subprocess.TimeoutExpired:self.send(504,{"error":"Hermes timed out"})
  except Exception as ex:
   print("Hermes adapter exception",type(ex).__name__,str(ex)[:200],flush=True);self.send(502,{"error":"Hermes adapter failed"})
  finally:LOCK.release()
ThreadingHTTPServer(("0.0.0.0",8082),Handler).serve_forever()
