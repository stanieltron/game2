import copy
import unittest
from pathlib import Path
import balance


class BalanceContractTests(unittest.TestCase):
    def setUp(self):
        self.spec = Path(__file__).parent / 'spec/targets.json'
        self.config = balance.read(self.spec)
        self.defs = balance.scenarios(self.spec, self.config)

    def fixture(self):
        result = {}
        for name, definition in self.defs.items():
            seconds = 6000
            if name == 'minimum':
                seconds *= 3
            elif definition['gate']:
                seconds *= 1 + self.config['targets']['penalties'][definition['gate']] + .01
            result[name] = {'success': True, 'seconds': seconds}
        return result

    def test_steep_completed_curve_passes(self):
        self.assertTrue(balance.score(self.fixture(), self.defs, self.config['targets'], True)['passed'])

    def test_flat_curve_fails_even_with_right_minimum_ratio(self):
        result = self.fixture()
        for name in result:
            if self.defs[name]['gate']:
                result[name]['seconds'] = 6001
        self.assertFalse(balance.score(result, self.defs, self.config['targets'], True)['passed'])

    def test_deadlock_is_not_a_good_overbuilding_penalty(self):
        result = self.fixture()
        result['over_6'] = {'success': False, 'seconds': 999999}
        self.assertFalse(balance.score(result, self.defs, self.config['targets'], True)['passed'])

    def test_refund_loophole_fails_validation(self):
        result = self.fixture()
        result['over_6_reclaim']['seconds'] = 5900
        self.assertTrue(balance.score(result, self.defs, self.config['targets'])['passed'])
        self.assertFalse(balance.score(result, self.defs, self.config['targets'], True)['passed'])

    def test_missing_challenger_cannot_pass(self):
        result = self.fixture()
        del result['smelter_7']
        self.assertFalse(balance.score(result, self.defs, self.config['targets'], True)['passed'])

    def test_six_extra_means_six_total_not_twelve(self):
        def total(name):
            return sum(x['count'] for x in self.defs[name]['policy']['stages'][2] if x['spec'] in ['structural', 'mechanical'])
        self.assertEqual(total('over_6') - total('reference'), 6)

    def test_repeat_definitions_and_input_conservation(self):
        self.assertEqual(balance.digest(self.defs), balance.digest(balance.scenarios(self.spec, self.config)))
        catalog = balance.read(balance.ROOT / 'Assets/Resources/catalog.json')
        original = copy.deepcopy(catalog)
        changed = balance.tune(catalog, {'capital': 2, 'cycles': .75})
        self.assertEqual(catalog, original)
        self.assertNotEqual(catalog, changed)

    def test_resume_normalizes_json_and_rejects_changed_physics(self):
        old = {'sourceHashes': {'engine.cs': 'a', 'report.py': 'b'}, 'widths': [1, 2, 3]}
        new = {'sourceHashes': {'engine.cs': 'a', 'report.py': 'c'}, 'widths': (1, 2, 3)}
        self.assertTrue(balance.compatible_manifest(old, new))
        new['sourceHashes']['engine.cs'] = 'changed'
        self.assertFalse(balance.compatible_manifest(old, new))


if __name__ == '__main__':
    unittest.main()
