"""Source contract tests; no fixture recipes or alternative kitchen simulation."""
import copy
import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('menu_generator', Path(__file__).with_name('generate-cooking-menu.py'))
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class SourceMappingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sheets, cls.audit = generator.source_audit()

    def test_source_row_reordering_cannot_change_runtime_identity_or_graph(self):
        before = generator.project(copy.deepcopy(self.sheets), copy.deepcopy(self.audit))
        reordered = {name: list(reversed(rows)) for name, rows in self.sheets.items()}
        after = generator.project(reordered, copy.deepcopy(self.audit))
        # Physical row provenance should change when rows move; semantic IDs and graph must not.
        before_nodes = before.pop('sourceNodes')
        after_nodes = after.pop('sourceNodes')
        self.assertEqual(before, after)
        def semantic_nodes(nodes):
            return sorted([{k: v for k, v in n.items() if k != 'excelRow'} for n in nodes], key=lambda n: n['sourceLocator'])
        self.assertEqual(semantic_nodes(before_nodes), semantic_nodes(after_nodes))

    def test_unknown_duplicate_and_mislabeled_source_ids_are_rejected(self):
        for change in ('unknown', 'duplicate', 'name'):
            sheets = copy.deepcopy(self.sheets)
            rows = sheets['供应原料']
            if change == 'unknown':
                rows[0][0] = 'UNKNOWN'
            elif change == 'duplicate':
                rows[1][0] = rows[0][0]
            else:
                rows[0][2], rows[1][2] = rows[1][2], rows[0][2]
            with self.assertRaises(ValueError):
                generator.project(sheets, copy.deepcopy(self.audit))

    def test_all_367_original_nodes_have_exact_traceable_disposition(self):
        audit = copy.deepcopy(self.audit)
        document = generator.project(copy.deepcopy(self.sheets), audit)
        recipes = {r['id'] for r in document['steps']}
        nodes = audit['nodeProjection']
        self.assertEqual(367, len(nodes))
        self.assertEqual(367, len({n['sourceLocator'] for n in nodes}))
        self.assertEqual(self.sheets['加工节点'], [n['sourceCells'] for n in nodes])
        for node in nodes:
            if node['classification'] == 'delivery-operation':
                self.assertEqual('SERVE', node['sourceNode'])
                self.assertEqual([], node['recipes'])
            else:
                self.assertTrue(node['recipes'])
                self.assertTrue(set(node['recipes']) <= recipes)

    def test_source_discrepancies_are_preserved_and_delivery_is_distinct(self):
        self.assertEqual(31, len(self.audit['differences']))
        for difference in self.audit['differences']:
            self.assertEqual('computedStationClosure', difference['field'])
            self.assertEqual({'贴单台'}, set(difference['computed']) - set(difference['excel']))
        for node in self.audit['nodes']:
            self.assertNotIn('贴单台', node['productionStations'])
            self.assertEqual(node['menu'].startswith('D'), '贴单台' in node['deliveryStations'])

    def test_load_bake_and_unmold_are_recipes_inside_the_same_actual_mold(self):
        document = generator.project(copy.deepcopy(self.sheets), copy.deepcopy(self.audit))
        for source in ('S04', 'S05', 'S06'):
            steps = [r for r in document['steps'] if r['sourceId'] == source]
            self.assertEqual(3, len(steps))
            self.assertEqual({'menu-container-cake-mold'}, {r['carrier'] for r in steps})
            for previous, following in zip(steps, steps[1:]):
                self.assertIn(previous['output'], [i['definition'] for i in following['inputs']])


if __name__ == '__main__':
    unittest.main()
