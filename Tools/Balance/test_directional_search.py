import unittest
import directional_search as d
import calibrate
import balance as b


class DirectionalTests(unittest.TestCase):
    def parent(self):return dict(id='baseline',parameters=calibrate.candidates(1)[0],decision='baseline',score=dict(loss=100))
    def test_worse_150_has_no_149_or_151_children(self):
        parent=self.parent();bad=dict(id='bad',parameters=dict(parent['parameters'],capital=1.5),score=dict(loss=150))
        self.assertIsNone(d.choose(parent,[bad]))
        self.assertEqual(d.children(bad,dict(capital=.01),set()),[])
    def test_only_measured_improvement_can_be_refined(self):
        parent=self.parent();good=dict(id='good',parameters=dict(parent['parameters'],capital=.5),score=dict(loss=80))
        bad=dict(id='bad',parameters=dict(parent['parameters'],capital=1.5),score=dict(loss=150))
        self.assertIs(d.choose(parent,[bad,good]),good)
        children=d.children(good,dict(capital=.25),set())
        self.assertTrue(children and all(r['parent']=='good' for r in children))
        self.assertEqual(d.children(bad,dict(capital=.01),set()),[])
    def test_reproducible_no_duplicate_proposals(self):
        p=self.parent();seen={b.digest(p['parameters'])}
        first=d.children(p,d.STEPS,seen)
        self.assertEqual(len(first),len({b.digest(r['parameters']) for r in first}))
        self.assertEqual(d.children(p,d.STEPS,seen),[])


if __name__=='__main__':unittest.main()
