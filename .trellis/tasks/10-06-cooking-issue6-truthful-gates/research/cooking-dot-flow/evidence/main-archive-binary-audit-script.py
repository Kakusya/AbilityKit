import argparse
import datetime
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser()
p.add_argument('--run', required=True)
p.add_argument('--native', required=True)
p.add_argument('--output', required=True)
a = p.parse_args()
root = Path('local/Logs/test-gates') / a.run
summary_path = root / 'gate-summary.json'
summary = json.loads(summary_path.read_text(encoding='utf-8-sig'))
native_path = Path(a.native)
native = json.loads(native_path.read_text(encoding='utf-8-sig'))
issues, checked, leaves = [], {}, []

def require(condition, reason):
    if not condition:
        issues.append(reason)

def hash_file(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1048576), b''):
            h.update(block)
    return h.hexdigest()

def binary_map(entries):
    result = {}
    for entry in entries:
        key = entry['path'].casefold()
        require(key not in result, 'duplicate binary identity: ' + entry['path'])
        result[key] = [entry['bytes'], entry['sha256']]
    return result

require(summary['runId'] == a.run, 'wrong root run')
require(summary['status'] == 'Passed' and summary['fullGateAccepted'] is True and summary['cliExitCode'] == 0, 'root acceptance')
require(native['processExitCode'] == 0 and native['exitConfirmed'] is True, 'parent exit')
require(summary['example'] is False, 'synthetic production result')
require(not summary['coverage']['missing'], 'missing coverage')
for node in [summary] + summary['children']:
    node_root = root / node['resultId'].replace('-', '')[:12]
    require(node['status'] == 'Passed' and node['cliExitCode'] == 0, 'non-passed node ' + node['name'])
    require(node['runId'] == a.run, 'wrong child run')
    require(node['source']['before']['sha'] == '68a880f2a31c7269da634b2f152362bccb5857ab', 'wrong source SHA')
    require(node['source']['before']['sha'] == node['source']['after']['sha'], 'source SHA changed')
    require(node['source']['before']['inputFingerprint'] == node['source']['after']['inputFingerprint'], 'source fingerprint changed')
    for artifact in node['artifacts']:
        require(artifact['runId'] == a.run and artifact['resultId'] == node['resultId'], 'artifact ownership')
        path = (node_root / artifact['path']).resolve()
        require(path.is_relative_to(node_root.resolve()), 'artifact escapes owned node')
        if path not in checked:
            checked[path] = [path.stat().st_size, hash_file(path)]
        require(checked[path] == [artifact['bytes'], artifact['sha256']], 'archive bytes/hash: ' + str(path))
    if node['kind'] == 'gate':
        continue
    prov = node['provenance']
    leaf = {'name': node['name'], 'kind': node['kind'], 'resultId': node['resultId'], 'buildOutputCount': len(prov['build']['outputs'])}
    outputs = binary_map(prov['build']['outputs'])
    require(prov['assembly'].casefold() in outputs, 'primary assembly missing from build')
    if node['kind'] == 'dotnet-test':
        maps = [outputs, binary_map(prov['loadedBefore']), binary_map(prov['loadedAfter']), binary_map(prov['test']['inputs'])]
        require(all(x == maps[0] for x in maps[1:]), 'binary set linkage: ' + node['name'])
        trx = [x for x in node['artifacts'] if x['role'] == 'trx']
        require(len(trx) == 1, 'unique TRX')
        xml = ET.parse(node_root / trx[0]['path']).getroot()
        counters = xml.find('.//{*}Counters').attrib
        entries = xml.findall('.//{*}UnitTestResult')
        definitions = xml.findall('.//{*}TestDefinitions/{*}UnitTest')
        ids = [(x.attrib['testId'], x.attrib['executionId']) for x in entries]
        require(len(ids) == len(set(ids)), 'duplicate actual TRX identity')
        require(len(entries) == len(definitions) == int(counters['total']) == int(counters['passed']) == int(counters['executed']) > 0, 'TRX nonzero full count')
        require(all(x.attrib['outcome'] == 'Passed' for x in entries), 'TRX non-pass')
        require(int(counters['failed']) == int(counters['notExecuted']) == 0, 'TRX failure/unexecuted')
        require(node['tests']['counts']['total'] == len(entries), 'normalized count mismatch')
        leaf.update(actualTrxCount=len(entries), filter=node['configuration']['filter'], binaryChainCount=len(outputs), assembly=prov['assembly'])
    else:
        require(node['tests'] is None, 'build claims tests')
    leaves.append(leaf)
report = {'status': 'Failed' if issues else 'Passed', 'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'runId': a.run, 'sourceSha': summary['source']['before']['sha'], 'sourceDirty': summary['source']['before']['dirty'], 'qualification': 'Historical archived bytes independently checked; binary linkage compares path/bytes/hash, not archive location. No new producer launched.', 'summarySha256': hash_file(summary_path), 'nativeSha256': hash_file(native_path), 'artifactCount': len(checked), 'artifactBytes': sum(x[0] for x in checked.values()), 'leaves': leaves, 'issues': issues}
output = Path(a.output)
assert not output.exists(), 'Do not overwrite independent evidence'
output.write_text(json.dumps(report, ensure_ascii=True, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=True))
raise SystemExit(bool(issues))
