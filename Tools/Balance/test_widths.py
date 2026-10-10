import unittest
import balance
import widths


class WidthSweepTests(unittest.TestCase):
    def setUp(self):
        self.catalog = balance.read(balance.ROOT / 'Assets/Resources/catalog.json')

    def test_grid_repeats_exactly_and_has_thousands_of_routes(self):
        a = widths.definitions(self.catalog, 48)
        b = widths.definitions(self.catalog, 48)
        self.assertEqual(balance.digest(a), balance.digest(b))
        self.assertEqual(len(a[0]), 48)
        self.assertGreater(a[2], 2000)
        self.assertEqual(len({balance.digest(v['policy']) for v in a[0].values()}), 48)

    def test_no_locked_building_is_requested(self):
        definitions, _, _ = widths.definitions(self.catalog, 0)
        techs = {b['id']: b['tech'] for b in self.catalog['buildings']}
        for definition in definitions.values():
            for stage, targets in enumerate(definition['policy']['stages']):
                for target in targets:
                    self.assertLessEqual(techs[target['spec'].split(':')[0]], stage)
                    self.assertGreater(target['count'], 0)

    def test_later_width_changes_actual_requested_capacity(self):
        a = widths.policy(self.catalog, (2, 2, 4, 6), 2, 'industry-first')
        b = widths.policy(self.catalog, (2, 2, 4, 12), 2, 'industry-first')
        self.assertEqual(a['stages'][:3], b['stages'][:3])
        counts_a = {x['spec']: x['count'] for x in a['stages'][3]}
        counts_b = {x['spec']: x['count'] for x in b['stages'][3]}
        self.assertEqual(counts_b['mechanical'], 2 * counts_a['mechanical'])
        self.assertGreater(counts_b['industrialGenerator'], counts_a['industrialGenerator'])


if __name__ == '__main__':
    unittest.main()
