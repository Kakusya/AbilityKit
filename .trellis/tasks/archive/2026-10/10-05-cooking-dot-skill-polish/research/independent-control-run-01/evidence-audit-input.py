# Exact stdin program previously run as PowerShell here-string | python -B -.
import json,hashlib,subprocess,re
from pathlib import Path
base=Path('.trellis/tasks/10-05-cooking-dot-skill-polish/research')
out=base/'independent-control-run-01'
r=json.loads((base/'controls-run-04/controls-output.json').read_text())
f=json.loads((base/'controls-run-04/controls-fixtures.json').read_text())
checks=[]
def c(name,expected,actual):checks.append({'name':name,'expected':expected,'actual':actual,'status':'Passed' if expected==actual else 'Failed'})
c('reported assertions with raw expected/actual',122,len([e for e in r['events'] if e.get('status')=='Passed' and 'expected' in e and 'actual' in e]))
c('all raw expectations actually equal',True,all(e['expected']==e['actual'] for e in r['events'] if 'expected' in e))
c('raw subprocess count',12,len(r['subprocesses']))
c('raw subprocess actual equals prescribed exit',True,all(p['exit']==p['expected_exit'] for p in r['subprocesses']))
c('intentional fault exits',[71,72,73,74],sorted(p['exit'] for p in r['subprocesses'] if p['exit']))
for prefix in ['lost-receipt','stale-index']:
 ledger=json.loads(f[prefix+'\\effects.json']);c(prefix+' persisted unique invocation',1,sum(ledger.values()));c(prefix+' persisted operation identity count',1,len(ledger))
for p in r['subprocesses']:
 if '--child' in p['command'] and 'recover' in p['command']:
  data=json.loads(p['stdout']);which=Path(p['command'][-1]).name
  c(which+' recovered durable boundary',[],data['index']['unresolved'])
  c(which+' recovery invocation count',0 if which=='pre-effect' else 1,sum(data['ledger'].values()))
 if '--child' in p['command'] and 'timing-read' in p['command']:
  d=json.loads(p['stdout']);c('raw restore cannot auto apply',False,d['can_apply']);c('raw restore preserves pause',True,d['preserve_pause'])
manifest=json.loads((base/'implementation-file-hashes.json').read_text())
freeze=json.loads((base/'implementation-freeze-receipt.json').read_text())
c('original manifest versus freeze bytes',manifest['file_sha256'],freeze['frozen_working_sha256'])
validator=(base/'original-validator-ram-stdout.txt').read_text(encoding='utf-8-sig');c('original stock validator raw success',True,'Skill is valid!' in validator)
prior=(base/'quick-validate-output.txt').read_bytes();c('original dependency failure retained',True,b'ModuleNotFoundError' in prior or 'ModuleNotFoundError' in prior.decode('utf-16'))
sha=freeze['candidate_sha'];links=[]
for path in freeze['git_blob_sha256']:
 if not path.endswith('.md'):continue
 txt=subprocess.check_output(['git','show',sha+':'+path]).decode()
 for target in re.findall(r'\]\(([^)]+)\)',txt):
  if '://' in target:continue
  part=target.split('#')[0]
  if not part:continue
  dest=(Path(path).parent/part).resolve().relative_to(Path.cwd()).as_posix()
  proc=subprocess.run(['git','cat-file','-e',sha+':'+dest],capture_output=True,text=True)
  links.append({'source':path,'target':target,'resolved':dest,'exit':proc.returncode})
c('frozen local link destinations exist',True,all(x['exit']==0 for x in links))
diff=subprocess.run(['git','diff','--check','4cf5b38bcb06b2f58d49fd73fc9de26e5cec02fe',sha,'--','.agents/skills/cooking-dot-workflow','.trellis/spec/abilitykit/supervised-issue-delivery.md'],capture_output=True,text=True)
(out/'frozen-diff-check.txt').write_text(diff.stdout+diff.stderr,encoding='utf-8')
result={'checks':checks,'links':links,'counts':{'checks':len(checks),'passed':sum(x['status']=='Passed' for x in checks),'failed':sum(x['status']=='Failed' for x in checks),'links':len(links),'original_assertions':r['passed'],'original_subprocesses':len(r['subprocesses'])},'diff_check':{'exit':diff.returncode,'raw':'frozen-diff-check.txt','interpretation':'Markdown hard breaks in two new templates; dirty diff claim did not cover untracked files'},'raw_source_sha256':{x:hashlib.sha256((base/x).read_bytes()).hexdigest() for x in ['controls-run-04/controls-output.json','controls-run-04/controls-fixtures.json','implementation-file-hashes.json','original-validator-ram-stdout.txt','quick-validate-output.txt']}}
(out/'original-evidence-audit.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result['counts']));print('frozen diff-check exit='+str(diff.returncode))
