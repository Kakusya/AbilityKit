"""Independent contract controls: isolated fixtures, frozen Git source, no live effects."""
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import types

ROOT = Path(__file__).resolve().parents[4]
RESEARCH = Path(__file__).resolve().parent
SHA = '32b400a5c6d375be366faa606e867ef68037cfa8'
HELPER = '.agents/skills/cooking-dot-workflow/scripts/records.py'
OUT = RESEARCH / 'independent-control-run-01'
OUT.mkdir(exist_ok=False)
results = []
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)
def digest(data):
    return hashlib.sha256(data).hexdigest()
def put(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding='utf-8')
def check(name, expected, actual):
    results.append(dict(name=name, expected=expected, actual=actual,
                        status='Passed' if expected == actual else 'Failed'))
def blocked(name, action):
    try:
        action()
    except (m.Invalid, KeyError, TypeError) as exc:
        check(name, 'Blocked', 'Blocked')
        results[-1]['reason'] = str(exc)
    else:
        check(name, 'Blocked', 'Allowed')
def snapshot(root):
    return {str(p.relative_to(root)): digest(p.read_bytes()) for p in root.rglob('*') if p.is_file()}

freeze = json.loads((RESEARCH/'implementation-freeze-receipt.json').read_text())
identity = {'candidate_sha': SHA, 'head': git('rev-parse','HEAD').decode().strip(),
            'branch': git('branch','--show-current').decode().strip(),
            'dirty_before': git('status','--short').decode(), 'python':sys.version,
            'executable':sys.executable, 'executable_sha256':digest(Path(sys.executable).read_bytes()),
            'script_sha256':digest(Path(__file__).read_bytes()), 'files':{}}
check('announced frozen identity', SHA, freeze['candidate_sha'])
for path, expected in freeze['git_blob_sha256'].items():
    blob = git('show', f'{SHA}:{path}')
    local = (ROOT/path).read_bytes()
    identity['files'][path] = {'blob':digest(blob),'local_before':digest(local),
                              'expected_blob':expected,'expected_local':freeze['frozen_working_sha256'][path]}
    check('frozen blob '+path, expected, digest(blob))
    check('working bytes '+path, freeze['frozen_working_sha256'][path], digest(local))
source = git('show', f'{SHA}:{HELPER}')
m = types.ModuleType('independent_frozen_records')
m.__file__ = str(ROOT/HELPER)
sys.modules[m.__name__] = m
exec(compile(source, f'{SHA}:{HELPER}', 'exec'), m.__dict__)

FLOW = 'b2ea381e-761d-4fc8-8d1c-9d34cab28d83'
def fixture(name):
    root = OUT/name
    for file in ('approval.txt','authority.txt','payload.txt','raw.txt'):
        (root/'evidence').mkdir(parents=True,exist_ok=True)
        (root/'evidence'/file).write_text('independent local simulation '+file,encoding='utf-8')
    flow = dict(version=1,skill='cooking-dot-workflow',flow_id=FLOW,canonical_root=str(root.resolve()),
                execution_host='local-sim',worktree_id='local-sim',task_path='.trellis/tasks/independent-fixture',
                repository='local-sim',branch='local-sim',target_branch='local-sim',delivery_mode='branch-only',
                original_session='original',original_terminal='original-terminal',run_id='run-sim',
                approval_ref='evidence/approval.txt',created_utc='2026-10-05T00:00:00Z')
    auth=m.WriteAuthority('original','original-terminal',True,True,'evidence/authority.txt')
    m.initialize(root,flow,auth)
    return root,flow,auth
def intent(flow,seq=1,action='dispatch',pre=None):
    return dict(version=1,flow_id=FLOW,seq=seq,op_id=f'{FLOW}:{seq:06d}',kind='intent',
                recorded_utc='2026-10-05T00:01:00Z',action=action,target='local-simulation',
                authorization_ref='evidence/approval.txt',payload_ref='evidence/payload.txt',
                preconditions=pre or {'run_id':'run-sim','task_id':'task-sim','dispatch_id':'dispatch-sim'})
def receipt(i,outcome='applied',result=None):
    return {**{k:i[k] for k in ('version','flow_id','seq','op_id')},'kind':'receipt',
            'recorded_utc':'2026-10-05T00:02:00Z','intent_hash':m.fingerprint(i),
            'outcome':outcome,'raw_ref':'evidence/raw.txt','result':result or {}}

r,f,a=fixture('canonical')
i=intent(f)
m.append(r,i,a)
check('intent absent from index still unresolved',[i['op_id']],m.reconstruct(r)['unresolved'])
blocked('unresolved effect prevents later external effect',lambda:m.append(r,intent(f,2),a))
for field,wrong in [('action','push'),('task_id','other-task'),('dispatch_id','old-dispatch'),('run_id','other-run')]:
    blocked('receipt rejects wrong '+field,lambda field=field,wrong=wrong:m.append(r,receipt(i,result={field:wrong}),a))
m.append(r,receipt(i,outcome='unknown'),a)
blocked('unknown receipt cannot be overwritten',lambda:m.append(r,receipt(i),a))
rec=intent(f,2,'reconcile',{'resolves':i['op_id']})
m.append(r,rec,a)
blocked('reconcile must correlate unresolved identity',lambda:m.append(r,receipt(rec,result={'resolves':'wrong-op'}),a))
m.append(r,receipt(rec,result={'resolves':i['op_id']}),a)
check('reconciliation leaves no unresolved effect',[],m.reconstruct(r)['unresolved'])
put(r/'index.json',{'last_seq':0})
before=snapshot(r)
check('stale index ignored',2,m.reconstruct(r)['last_seq'])
check('read-only reconstruction keeps bytes',before,snapshot(r))
for label,auth in [('new-session',m.WriteAuthority('new','original-terminal',True,True,'evidence/authority.txt')),
                   ('unverified',m.WriteAuthority('original','original-terminal',False,True,'evidence/authority.txt')),
                   ('no-record-permission',m.WriteAuthority('original','original-terminal',True,False,'evidence/authority.txt'))]:
    blocked(label+' cannot index-repair',lambda auth=auth:m.write_index(r,auth))
    blocked(label+' cannot append',lambda auth=auth:m.append(r,intent(f,3),auth))
check('denied mutation preserves canonical bytes',before,snapshot(r))

copyroot=OUT/'old-copy'
import shutil
shutil.copytree(r,copyroot)
(copyroot/'records/000002.intent.json').unlink()
(copyroot/'records/000002.receipt.json').unlink()
check('old subset is evidence', 'old-subset',m.copies([r,copyroot])[FLOW][1]['copy'])
blocked('copied owner identity cannot write index',lambda:m.write_index(copyroot,a))
bad=m.read(copyroot/'records/000001.intent.json');bad['target']='other-effect'
put(copyroot/'records/000001.intent.json',bad)
blocked('conflicting copy blocks authority election',lambda:m.copies([r,copyroot]))

r2,f2,a2=fixture('second-self-canonical')
blocked('same flow cannot declare two canonical authorities',lambda:m.copies([r,r2]))
missing=f2.copy();missing['canonical_root']=str(OUT/'absent-authority')
put(r2/'flow.json',missing)
blocked('missing canonical does not promote copy',lambda:m.copies([r2]))

request=dict(request_id='req-independent',flow_id=FLOW,type='final-review',candidate_sha=SHA,paused=False,
             conversation_id='conversation-independent',prior_message_boundary='prior-message',
             request_message_id='request-message',wait_started_utc='2026-10-05T00:00:00Z',
             deadline_utc='2026-10-05T00:30:00Z')
reply=dict(full=True,generating=False,decision='accept-candidate',request_id=request['request_id'],flow_id=FLOW,
           reviewed_sha=SHA,conversation_id=request['conversation_id'],message_id='reply-message',
           after_request_verified=True,absent_at_prior_boundary=True,time_reliable=False,reply_posted_utc=None)
proof={k:reply[k] for k in ('request_id','flow_id','conversation_id','message_id','reviewed_sha')}
proof.update(verified=True,clock_trusted=True,clock_jump=False,full=True,generating=False,
             prior_message_boundary='prior-message',full_snapshot_ref='evidence/full.txt',
             prior_snapshot_ref='evidence/prior.txt',time_source='controlled UTC',uncertainty_ms=0,
             observed_utc='2026-10-05T00:29:59Z')
reply['timing_proof']=proof
check('complete observation without posted time is timely',True,m.reply_decision(request,reply,'2026-10-05T00:29:59Z')['can_apply'])
for label,change in [('fragment',{'full':False}),('generating',{'generating':True}),
                     ('wrong-conversation',{'conversation_id':'other'}),('wrong-request',{'request_id':'other'}),
                     ('wrong-sha',{'reviewed_sha':'0'*40}),('old-boundary',{'absent_at_prior_boundary':False})]:
    altered=copy.deepcopy(reply);altered.update(change)
    check(label+' cannot accept',False,m.reply_decision(request,altered,'2026-10-05T00:29:59Z')['valid_resolution'])
for label,change in [('clock-crosses-deadline',{'uncertainty_ms':1001}),('clock-jump',{'clock_jump':True}),
                     ('unverified-proof',{'verified':False}),('fragment-proof',{'full':False}),
                     ('other-message-proof',{'message_id':'another-message'}),('other-boundary',{'prior_message_boundary':'other'})]:
    altered=copy.deepcopy(reply);altered['timing_proof'].update(change)
    check(label+' cannot prove timely',False,m.reply_decision(request,altered,'2026-10-05T00:29:59Z')['valid_resolution'])
altered=copy.deepcopy(reply);altered['timing_proof']['observed_utc']='2026-10-05T00:30:01Z'
check('fragment-before/full-after cannot borrow earlier time',False,m.reply_decision(request,altered,'2026-10-05T00:30:01Z')['valid_resolution'])
late=m.reply_decision(request,reply,'2026-10-06T00:00:00Z')
check('durable earlier complete reply remains technically valid',True,late['valid_resolution'])
check('expired unsaved pause cannot auto apply',False,late['can_apply'])
check('expired unsaved pause must be saved',True,late['pause_required'])
check('expired unsaved pause preserved',True,late['preserve_pause'])
posted=copy.deepcopy(reply);posted.update(time_reliable=True,reply_posted_utc='2026-10-05T00:20:00Z')
check('timely posted but late read cannot apply',False,m.reply_decision(request,posted,'2026-10-06T00:00:00Z')['can_apply'])
posted['reply_posted_utc']='2026-10-05T00:31:00Z'
check('genuinely late reply invalid',False,m.reply_decision(request,posted,'2026-10-06T00:00:00Z')['valid_resolution'])
unknown=copy.deepcopy(reply);unknown.pop('timing_proof')
check('first full after deadline with unknown posted time invalid',False,m.reply_decision(request,unknown,'2026-10-06T00:00:00Z')['valid_resolution'])
supp=intent(f,action='dot-supplement',pre={'request_id':request['request_id']})
check('unused supplement eligible',True,m.supplement_allowed(request,[],'2026-10-05T00:10:00Z',False))
check('lost supplement receipt still consumes quota',False,m.supplement_allowed(request,[supp],'2026-10-05T00:10:00Z',False))
check('unknown generation refuses supplement',False,m.supplement_allowed(request,[],'2026-10-05T00:10:00Z',None))
new=copy.deepcopy(request);new.update(request_id='reopened',parent_request_id=request['request_id'],
                                    wait_started_utc='2026-10-06T00:00:00Z',deadline_utc='2026-10-06T00:30:00Z')
revision=copy.deepcopy(reply);revision.update(decision='needs-revision',observed_utc='2026-10-06T00:00:00Z')
check('late-read revision cannot refresh original budget',False,m.reopen_allowed(request,new,revision_decision=revision))
check('explicit owner reopening permits linked new budget',True,m.reopen_allowed(request,new,owner_resume=True))

gate=dict(mode='branch-only',paused=False,candidate_sha=SHA,accepted_sha=SHA,
          **{k:True for k in ('scope_checked','independent_check','workers_accounted','required_checks_passed',
                             'candidate_accepted','hashes_unchanged','effects_reconciled','branch_push_verified','required_pilot_passed')})
check('bounded branch completion without merge',True,m.delivery_allowed(gate))
for key,value in [('paused',True),('accepted_sha','0'*40),('required_pilot_passed',False),
                  ('workers_accounted',False),('effects_reconciled',False),('required_checks_passed',False)]:
    check('completion refuses '+key,False,m.delivery_allowed({**gate,key:value}))
merge=dict(mode='production',**{k:True for k in ('authorized','head_base_current','candidate_accepted',
           'required_checks_passed','scope_checked','sole_coordinator_verified','closing_links_inspected','closing_links_clear','effects_reconciled')})
check('production premerge clear simulation',True,m.premerge_allowed(merge))
for key,value in [('mode','branch-only'),('closing_links_clear',False),('closing_links_clear',None),
                  ('closing_links_inspected',False),('authorized',False),('effects_reconciled',False)]:
    check('merge refuses '+key+'='+str(value),False,m.premerge_allowed({**merge,key:value}))
production={**gate,'mode':'production','premerge_verified':True,'merge_verified':True,
            'integration_passed':True,'issue_required':True,'issue_close_verified':True}
check('production complete simulation',True,m.delivery_allowed(production))
for key in ('integration_passed','merge_verified','premerge_verified','issue_close_verified'):
    check('production incomplete '+key,False,m.delivery_allowed({**production,key:False}))

cli=subprocess.run([sys.executable,'-B',str(ROOT/HELPER),'inspect',str(r)],capture_output=True,text=True)
check('actual read-only diagnostic CLI exit',0,cli.returncode)
check('actual read-only diagnostic CLI unresolved',[],json.loads(cli.stdout)['unresolved'])
identity['diagnostic_cli']={'argv':cli.args,'exit':cli.returncode,'stdout':cli.stdout,'stderr':cli.stderr}
check('CLI preserves stale canonical index',before,snapshot(r))
for path in identity['files']:
    identity['files'][path]['local_after']=digest((ROOT/path).read_bytes())
identity['dirty_after']=git('status','--short').decode()
put(OUT/'source-identity.json',identity)
put(OUT/'request-reply-fixtures.json',{'request':request,'reply':reply,'late_read_actual':late})
summary={'source_sha':SHA,'envelope':'isolated local simulation and read-only actual subprocess diagnostic',
         'count':len(results),'passed':sum(x['status']=='Passed' for x in results),
         'failed':sum(x['status']=='Failed' for x in results),'results':results}
put(OUT/'results.json',summary)
print(json.dumps({k:v for k,v in summary.items() if k!='results'},indent=2))
for item in results:
    if item['status']=='Failed':print(json.dumps(item))
sys.exit(1 if summary['failed'] else 0)
