"""Deterministic read-only v0.1 source audit and production menu projection.

No Excel dependency: reads OOXML using zip/XML. Original source bytes are never changed.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import re
import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Docs/design/CookingGame/reference/menu-v0.1'
TASK = ROOT / '.trellis/tasks/10-02-cooking-singleplayer-menu-schema'
NS = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
IDENTITIES = Path(__file__).with_name('cooking-menu-source-ids.json')


def source_identities(sheets):
    """Source IDs, never row positions, own identities; names detect swapped/mislabeled rows."""
    mapping = json.loads(IDENTITIES.read_text(encoding='utf-8'))
    for table, name_index in [('供应原料', 2), ('半成品', 1), ('菜品目录', 3), ('工位', 1)]:
        rows = sheets[table]
        if len({r[0] for r in rows}) != len(rows):
            raise ValueError(f'Duplicate source ID: {table}')
        if {r[0] for r in rows} != set(mapping[table]):
            raise ValueError(f'Unknown or missing source ID: {table}')
        if len({r[name_index] for r in rows}) != len(rows):
            raise ValueError(f'Duplicate source name: {table}')
        for row in rows:
            if row[name_index] != mapping[table][row[0]]['name']:
                raise ValueError(f'Source identity/name conflict: {table}/{row[0]}')
        if len({r['slug'] for r in mapping[table].values()}) != len(mapping[table]):
            raise ValueError(f'Duplicate runtime slug: {table}')
    return mapping


def workbook(path):
    with zipfile.ZipFile(path) as archive:
        strings = [''.join(x.itertext()) for x in ET.fromstring(archive.read('xl/sharedStrings.xml')).findall('m:si', NS)]
        result = {}
        for number, name in enumerate(['说明', '菜品目录', '加工节点', '半成品', '供应原料', '工位', '参考来源'], 1):
            rows = []
            for row in ET.fromstring(archive.read(f'xl/worksheets/sheet{number}.xml')).findall('m:sheetData/m:row', NS):
                values = []
                for cell in row:
                    value = cell.find('m:v', NS)
                    values.append(strings[int(value.text)] if value is not None and cell.get('t') == 's' else value.text if value is not None else '')
                rows.append(values)
            result[name] = rows[4:]
        return result


def portions(value):
    result = []
    for part in value.split(' + '):
        match = re.fullmatch(r'(.+?)×([1-9][0-9]*)', part)
        if not match:
            raise ValueError(f'Unparsed input: {value}')
        result.append((match[1], int(match[2])))
    return result


def source_audit():
    markdown = next(SOURCE.glob('Cooking*.md'))
    excel = next(SOURCE.glob('*.xlsx'))
    text = markdown.read_text(encoding='utf-8')
    sheets = workbook(excel)
    md_rows = {}
    for line in text.splitlines():
        if re.match(r'\| [FSDP][0-9]{2} \|', line):
            values = [x.strip() for x in line.strip('|').split('|')]
            if values[0] in md_rows:
                raise ValueError(f'Duplicate Markdown source ID: {values[0]}')
            md_rows[values[0]] = values
    findings = []

    def compare(table, key, field, left, right, computed=False):
        if left != right:
            findings.append(dict(table=table, record=key, field=field, **({'computed': left} if computed else {'markdown': left}), excel=right))

    for row in sheets['菜品目录']:
        md = md_rows[row[0]]
        for field, a, b in [('name', md[1], row[3]), ('inputs', md[2], row[4]), ('operation', md[3], row[5]), ('container', md[5].split(' / ')[1], row[8]), ('appearance', md[5].split(' / ')[0], row[7]), ('handoff', md[6], row[9])]:
            compare('菜品目录', row[0], field, a, b)
    for row in sheets['半成品']:
        md = md_rows[row[0]]
        for index, field in enumerate(['id', 'name', 'inputs', 'operation', 'stations', 'carrier']):
            compare('半成品', row[0], field, md[index], row[index])
    md_supply_section = text.split('## 四、供应原料')[1].split('## 五、工位目录')[0]
    md_supplies = [item for line in md_supply_section.splitlines() if '、' in line and not line.startswith('以下') for item in line.rstrip('。').split('、')]
    compare('供应原料', 'ALL', 'names', sorted(md_supplies), sorted(row[2] for row in sheets['供应原料']))
    md_stations = [[x.strip() for x in line.strip('|').split('|')] for line in text.split('## 五、工位目录')[1].split('## 六、')[0].splitlines() if line.startswith('| ')][1:]
    for row in sheets['工位']:
        md = next(x for x in md_stations if x[0] == row[1])
        for index, field in enumerate(['name', 'capabilities', 'boundary']):
            compare('工位', row[0], field, md[index], row[index + 1])
    preparations = {r[1]: r for r in sheets['半成品']}
    supplies = {r[2]: r for r in sheets['供应原料']}

    def closure(inputs, trail=()):
        raw, prep = set(), set()
        for name, _ in portions(inputs):
            if name in supplies:
                raw.add(name)
            elif name in preparations:
                row = preparations[name]
                if name in trail:
                    raise ValueError(f'Cycle {trail} -> {name}')
                prep.add(row[0])
                more_raw, more_prep = closure(row[2], trail + (name,))
                raw.update(more_raw)
                prep.update(more_prep)
            else:
                raise ValueError(f'Missing source {name}')
        return raw, prep

    node_checks = []
    for row in sheets['菜品目录']:
        raw, prep = closure(row[4])
        nodes = [n for n in sheets['加工节点'] if n[0] == row[0]]
        actual_prep = {n[2] for n in nodes if n[2].startswith('P')}
        compare('加工节点', row[0], 'preparationClosure', sorted(prep), sorted(actual_prep), True)
        compare('菜品目录', row[0], 'supplyClosure', sorted(raw), sorted(row[10].split('、')), True)
        stations = {s for n in nodes if n[2] != 'SERVE' for s in n[7].split('、')}
        production_stations = sorted(stations)
        if row[0].startswith('D'):
            stations.add('贴单台')
        compare('菜品目录', row[0], 'computedStationClosure', sorted(stations), sorted(row[6].split('、')), True)
        if len(nodes) != len(actual_prep) + 2 or {n[2] for n in nodes if not n[2].startswith('P')} != {'FINAL', 'SERVE'}:
            raise ValueError(f'Duplicate or missing nodes: {row[0]}')
        for node in nodes:
            if node[2] != 'SERVE':
                expected = []
                for name, _ in portions(node[5]):
                    expected.append(preparations[name][0] + ' ' + name if name in preparations else '供货：' + name)
                compare('加工节点', row[0] + '/' + node[2], 'upstream', sorted(expected), sorted(node[9].split('；')), True)
            if node[2].startswith('P'):
                prep_row = preparations[node[4]]
                for a, b, field in [(node[2], prep_row[0], 'id'), (node[5], prep_row[2], 'inputs'), (node[6], prep_row[3], 'operation'), (node[7], prep_row[4], 'stations'), (node[8], prep_row[5], 'carrier')]:
                    compare('加工节点', row[0] + '/' + node[2], field, b, a)
            elif node[2] == 'FINAL':
                for a, b, field in [(node[5], row[4], 'inputs'), (node[6], row[5], 'operation'), (node[8], row[8], 'carrier')]:
                    compare('加工节点', row[0] + '/FINAL', field, b, a)
            elif node[2] != 'SERVE':
                raise ValueError(f'Unknown node {node}')
            else:
                compare('加工节点', row[0] + '/SERVE', 'upstream', 'FINAL', node[9], True)
                compare('加工节点', row[0] + '/SERVE', 'carrier', row[8], node[8], True)
                compare('加工节点', row[0] + '/SERVE', 'station', '贴单台' if row[0].startswith('D') else '出餐点', node[7], True)
        node_checks.append(dict(menu=row[0], sourceNodes=len(nodes), preparations=sorted(prep), supplies=sorted(raw),
            productionStations=production_stations, deliveryStations=['贴单台', '出餐点'] if row[0].startswith('D') else ['出餐点'], stations=sorted(stations)))
    for row in sheets['半成品']:
        raw, _ = closure(row[2])
        compare('半成品', row[0], 'supplyClosure', sorted(raw), sorted(row[7].split('、')), True)
    return sheets, dict(sources=[dict(path=str(p.relative_to(ROOT)).replace('\\', '/'), sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in [markdown, excel]], counts={name: len(rows) for name, rows in sheets.items()}, differences=findings, nodes=node_checks, rawSheets=sheets)


CONTAINER_SLUGS = {'备料盒': 'prep-box', '备料盘': 'prep-plate', '煮锅': 'cook-pot', '饭桶': 'rice-tub', '备料盆': 'prep-bowl', '酱料盆': 'sauce-bowl', '平底锅': 'pan', '搅拌盆': 'mix-bowl', '配料罐': 'topping-jar', '热水杯': 'water-cup', '壶': 'water-jug', '果汁壶': 'juice-jug', '茶桶': 'tea-tub', '粉盒': 'powder-box', '接液杯': 'extract-cup', '奶缸': 'milk-jug', '小料罐': 'pearl-jar', '调饮壶': 'drink-jug', '饮品杯': 'dispense-cup', '餐盘': 'plate', '汤碗': 'soup-bowl', '披萨盘': 'pizza-plate', '蒸食盘': 'steam-plate', '大餐盘': 'large-plate', '出餐小锅': 'serve-pot', '甜品盘': 'dessert-plate', '甜品杯': 'dessert-cup', '透明冷饮杯': 'cold-cup', '热饮杯': 'hot-cup', '烤盘': 'bake-tray', '模具': 'cake-mold', '蒸篮': 'steam-basket', '炸篮': 'fry-basket', '烤串架': 'skewer-tray'}

# Each group consumes only named source inputs plus the previous group's output.
# These expand the source's compound FINAL/preparation rows into real recipe links.
# No arithmetic changes to the original game input portions are permitted.
STAGES = {
    'P31': [('assemble', [0, 1]), ('fry', [])],
    'P32': [('assemble', [0, 1]), ('fry', [])],
    'P33': [('assemble', [0, 1]), ('fry', [])],
    'P36': [('pan-cook', [0]), ('pan-cook', [1])],
    'P37': [('pan-cook', [0]), ('pan-cook', [1])],
    'F02': [('assemble', [0, 1, 2]), ('assemble', [3])],
    'F03': [('assemble', [0, 1]), ('assemble', [2])],
    'F05': [('boil', [0, 1, 2]), ('assemble', [3])],
    'F15': [('assemble', [0, 1]), ('assemble', [2])],
    'F16': [('assemble', [0, 1]), ('assemble', [2, 3])],
    'S01': [('pan-cook', [0]), ('assemble', [1])],
    'S02': [('pan-cook', [0]), ('assemble', [1])],
    'S03': [('pan-cook', [0]), ('assemble', [2]), ('assemble', [1])],
    'S04': [('assemble', [0]), ('bake', []), ('assemble', [])],
    'S05': [('mix', [0, 1]), ('bake', []), ('assemble', [])],
    'S06': [('mix', [0, 1]), ('bake', []), ('assemble', [])],
    'S07': [('fry', [0]), ('assemble', [1])],
    'S08': [('fry', [0]), ('assemble', [1])],
    'S11': [('grill', [0]), ('assemble', [1, 2])],
    'S12': [('assemble', [0]), ('assemble', [1, 2]), ('assemble', [3])],
    'D01': [('drink-mix', [1]), ('drink-mix', [0])],
    'D02': [('drink-mix', [1, 2]), ('drink-mix', [3]), ('drink-mix', [0])],
    'D03': [('drink-mix', [1]), ('drink-mix', [2]), ('drink-mix', [0])],
    'D05': [('drink-mix', [0, 1, 2]), ('drink-mix', [3])],
    'D06': [('drink-mix', [0, 1]), ('drink-mix', [2])],
    'D14': [('drink-mix', [0, 1]), ('drink-mix', [2, 3])],
    'D18': [('drink-mix', [0, 1]), ('drink-mix', [2])],
    'D19': [('drink-mix', [1, 2]), ('drink-mix', [0])],
    'D20': [('drink-mix', [0, 1, 2]), ('drink-mix', [3])],
    'D23': [('drink-mix', [0, 2]), ('drink-mix', [1, 3])],
    'D24': [('drink-mix', [0]), ('drink-mix', [1])],
    'D25': [('drink-mix', [1]), ('drink-mix', [0]), ('drink-mix', [2])],
    'D26': [('drink-mix', [1]), ('drink-mix', [0]), ('drink-mix', [2])],
    'D27': [('drink-mix', [1]), ('drink-mix', [0]), ('drink-mix', [2])],
    'D28': [('drink-mix', [0]), ('drink-mix', [1]), ('drink-mix', [2])],
    'D29': [('drink-mix', [0, 1, 2]), ('drink-mix', [3])],
    'D30': [('drink-mix', [0, 1, 2]), ('drink-mix', [3])],
    'D31': [('drink-mix', [0]), ('drink-mix', [2]), ('drink-mix', [1])],
}


def project(sheets, audit):
    identities = source_identities(sheets)
    sheets = {name: sorted(rows, key=lambda r: r[0]) if name != '加工节点' else rows for name, rows in sheets.items()}
    material_ids, materials, steps, menus, containers = {}, [], [], [], {}
    capabilities = {}
    for row in sheets['工位']:
        cap = identities['工位'][row[0]]['slug']
        caps = [cap, 'foam-milk'] if row[0] == 'T16' else [cap]
        capabilities[row[1]] = caps
    for table, kind, name_index in [('供应原料', 'Supply', 2), ('半成品', 'Preparation', 1), ('菜品目录', 'Finished', 3)]:
        for row in sheets[table]:
            slug = identities[table][row[0]]['slug']
            id = f'menu-{kind.lower()}-{slug}'
            materials.append(dict(id=id, sourceId=row[0], name=row[name_index], kind=kind))
            if kind != 'Finished':
                if row[name_index] in material_ids:
                    raise ValueError(f'Ambiguous supply/preparation name: {row[name_index]}')
                material_ids[row[name_index]] = id

    def carrier(name):
        id = 'menu-container-' + CONTAINER_SLUGS[name]
        containers.setdefault(id, dict(id=id, name=name, capacity=1, acceptedDefinitions=[], disposable=name in {'透明冷饮杯', '热饮杯'}))
        return id

    cap_carriers = {'chop': '备料盒', 'assemble': '搅拌盆', 'boil': '煮锅', 'pan-cook': '平底锅', 'bake': '烤盘', 'steam': '蒸篮', 'fry': '炸篮', 'grill': '烤串架', 'mix': '搅拌盆', 'juice': '果汁壶', 'blend': '调饮壶', 'drink-mix': '调饮壶', 'brew': '茶桶', 'grind': '粉盒', 'extract': '接液杯', 'heat-milk': '奶缸', 'foam-milk': '奶缸', 'hot-water': '壶', 'dispense': '饮品杯'}
    final_nodes = {r[0]: r for r in sheets['加工节点'] if r[2] == 'FINAL'}
    batch_preps = {'P19', 'P20', 'P35', 'P36', 'P37', 'P38', 'P39', 'P40', 'P42', 'P43', 'P44', 'P45', 'P46', 'P47', 'P48', 'P53', 'P54', 'P55', 'P56', 'P57', 'P60'}
    for row in sheets['半成品'] + sheets['菜品目录']:
        prep = row[0].startswith('P')
        key, name = row[0], row[1] if prep else row[3]
        inputs = portions(row[2] if prep else row[4])
        if prep:
            output = material_ids[name]
            source_stations = row[4].split('、')
        else:
            output = next(m['id'] for m in materials if m['sourceId'] == key and m['kind'] == 'Finished')
            source_stations = final_nodes[key][7].split('、')
        plan = STAGES.get(key)
        if plan is None:
            caps = [capabilities[s][0] for s in source_stations]
            if key == 'P52':
                caps = ['foam-milk']
            if key.startswith('F') and key in {f'F{i:02}' for i in range(17, 21)} | {f'F{i:02}' for i in range(28, 31)} | {f'F{i:02}' for i in range(36, 41)}:
                caps = ['assemble', caps[-1]]
            if key == 'S10':
                caps = ['assemble', 'bake']
            plan = [(cap, list(range(len(inputs))) if index == 0 else []) for index, cap in enumerate(caps)]
        assert sorted(i for _, indices in plan for i in indices) == list(range(len(inputs))), key
        previous = None
        for index, (cap, indices) in enumerate(plan):
            last = index == len(plan) - 1
            product = output if last else output + f'-stage-{index + 1}'
            if not last:
                materials.append(dict(id=product, sourceId=key + f'/STAGE{index + 1}', name=name + f' stage {index + 1}', kind='Stage'))
            current_inputs = [dict(definition=material_ids[inputs[i][0]], portions=inputs[i][1]) for i in indices]
            if previous:
                current_inputs.insert(0, dict(definition=previous, portions=1))
            # Loading, heating and unloading all happen inside the real processing vessel.
            # The container persists; only the stage product is moved between vessels.
            thermal = next((c for c, _ in plan if c in {'bake', 'steam', 'fry', 'grill'}), None)
            vessel = ('模具' if key in {'S04', 'S05', 'S06'} else cap_carriers[thermal]) if thermal else None
            container = carrier(vessel or cap_carriers[cap])
            accepted = containers[container]['acceptedDefinitions']
            accepted.extend(i['definition'] for i in current_inputs)
            accepted.append(product)
            containers[container]['capacity'] = max(containers[container]['capacity'], sum(i['portions'] for i in current_inputs))
            step_id = 'menu-recipe-' + output.removeprefix('menu-') + ('' if last else f'-stage-{index + 1}')
            steps.append(dict(id=step_id, sourceId=key, sourceLocator=('半成品/' if prep else '加工节点/' + key + '/') + (key if prep else 'FINAL'), inputs=current_inputs, output=product, process='menu-process-' + cap, capability='menu-capability-' + cap, carrier=container, executionKind='Manual' if cap in {'chop', 'assemble', 'drink-mix'} else 'Automatic', requiredTicks=1, yieldPortions=2 if last and key in batch_preps else 1, mustLast=not prep and last and len(plan) > 1, operation=row[3] if prep else row[5]))
            previous = product
        if not prep:
            serving = carrier(row[8])
            containers[serving]['acceptedDefinitions'].append(output)
            menus.append(dict(sourceId=key, name=name, category={'F': 'Meal', 'S': 'Dessert', 'D': 'Drink'}[key[0]], product=output, finalRecipe=steps[-1]['id'], servingContainer=serving, orderTemplate='menu-order-' + output.removeprefix('menu-finished-'), requiresBinding=key.startswith('D'), baseScore=100, sourceLocator='菜品目录/' + key, requiresStagedFinal=len(plan) > 1, finalAdditions=[material_ids[inputs[i][0]] for i in plan[-1][1]] if len(plan) > 1 else []))
    for container in containers.values():
        container['acceptedDefinitions'] = sorted(set(container['acceptedDefinitions']))
    # Independently prove expanded stage chains preserve the original direct multiset.
    by_output = {s['output']: s for s in steps}
    by_material = {m['id']: m for m in materials}
    def unstage(id, multiplier=1):
        if by_material[id]['kind'] != 'Stage':
            return [id] * multiplier
        return [value for i in by_output[id]['inputs'] for value in unstage(i['definition'], multiplier * i['portions'])]
    for row in sheets['半成品'] + sheets['菜品目录']:
        final = next(s for s in steps if s['sourceId'] == row[0] and by_material[s['output']]['kind'] != 'Stage')
        expected = sorted(material_ids[name] for name, count in portions(row[2] if row[0].startswith('P') else row[4]) for _ in range(count))
        actual = sorted(value for i in final['inputs'] for value in unstage(i['definition'], i['portions']))
        assert expected == actual, (row[0], expected, actual)
    audit['nodeProjection'] = [dict(menu=n[0], sourceNode=n[2], excelRow=index + 5,
        sourceLocator=f'加工节点/{n[0]}/{n[2]}', sourceCells=n,
        classification='delivery-operation' if n[2] == 'SERVE' else 'shared-preparation' if n[2].startswith('P') else 'expanded-final' if len([s for s in steps if s['sourceId'] == n[0]]) > 1 else 'final-recipe',
        recipes=[s['id'] for s in steps if s['sourceId'] == (n[0] if n[2] == 'FINAL' else n[2])],
        deliveryPolicy='bind-then-submit' if n[0].startswith('D') else 'submit-without-binding')
        for index, n in enumerate(sheets['加工节点'])]
    audit['expandedRows'] = [dict(sourceId=row[0], operation=row[3] if row[0].startswith('P') else row[5],
        recipes=[s['id'] for s in steps if s['sourceId'] == row[0]])
        for row in sheets['半成品'] + sheets['菜品目录'] if sum(s['sourceId'] == row[0] for s in steps) > 1]
    if len(audit['nodeProjection']) != 367 or any(not n['recipes'] and n['classification'] != 'delivery-operation' for n in audit['nodeProjection']):
        raise ValueError('Incomplete source node projection')
    audit['identityMapping'] = identities
    return dict(schema='cooking-menu-catalog-v1', valueStatus='fixture-defaults-not-balanced', sources=audit['sources'], decisions=['source-id-owned-identities', 'compound-rows-expanded-into-linked-recipes', 'drink-binding-is-delivery-dependency', 'batch-fixture-yield-two', 'all-other-fixture-yield-one', 'fixture-ticks-one-score-100', 'original-sources-preserved'], materials=materials, stations=[dict(sourceId=r[0], id='menu-station-' + identities['工位'][r[0]]['slug'], name=r[1], capabilities=['menu-capability-' + c for c in capabilities[r[1]]]) for r in sheets['工位']], containers=list(containers.values()), steps=steps, menus=menus, sourceNodes=audit['nodeProjection'])


def write(path, value, check):
    content = json.dumps(value, ensure_ascii=False, indent=2) + '\n'
    if check:
        if not path.exists() or path.read_text(encoding='utf-8') != content:
            raise SystemExit(f'Generated file differs: {path.relative_to(ROOT)}')
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8', newline='\n')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    sheets, audit = source_audit()
    catalog = project(sheets, audit)
    write(TASK / 'research/source-audit.json', audit, args.check)
    write(ROOT / 'src/AbilityKit.Game.Cooking/Content/menu-catalog-v1.json', catalog, args.check)
    print(f"Audited {audit['counts']}; differences={len(audit['differences'])}")


if __name__ == '__main__':
    main()
