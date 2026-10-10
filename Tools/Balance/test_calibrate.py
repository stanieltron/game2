import copy
import unittest
import balance
import calibrate


class CalibrationTests(unittest.TestCase):
    def test_repeatable_distinct_candidates(self):
        a=calibrate.candidates(128)
        self.assertEqual(a,calibrate.candidates(128))
        self.assertEqual(len({balance.digest(x) for x in a}),128)
        self.assertTrue(all(0<=x['refund']<=1 for x in a))

    def test_baseline_preserves_actual_catalog(self):
        c=balance.read(balance.ROOT/'Assets/Resources/catalog.json')
        tuned=balance.tune(c,calibrate.candidates(1)[0])
        tuned.pop('buildingRefund')
        self.assertEqual(c,tuned)

    def test_parameters_reach_physics_without_mutating_input(self):
        c=balance.read(balance.ROOT/'Assets/Resources/catalog.json');before=copy.deepcopy(c)
        p=dict(calibrate.candidates(1)[0],refund=.25,power=2,feedSpeed=2,cycles=.5,capital=2)
        changed=balance.tune(c,p)
        self.assertEqual(c,before)
        self.assertEqual(changed['buildingRefund'],.25)
        old={x['id']:x for x in c['buildings']};new={x['id']:x for x in changed['buildings']}
        self.assertEqual(new['structural']['power'],old['structural']['power']*2)
        self.assertEqual(new['industrialMiner']['rate'],old['industrialMiner']['rate']*2)
        oldr={x['id']:x for x in c['recipes']}
        self.assertTrue(all(x['seconds']==oldr[x['id']]['seconds']/2 for x in changed['recipes'] if x['family']=='industrialSmelter'))

    def test_reject_invalid_refund(self):
        c=balance.read(balance.ROOT/'Assets/Resources/catalog.json')
        with self.assertRaises(ValueError):balance.tune(c,dict(capital=1,cycles=1,refund=1.1))


if __name__=='__main__':unittest.main()
