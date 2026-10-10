using System;
using System.Collections.Generic;
namespace FactoryBalance
{
    public class BuildTarget {public string spec;public int count;public BuildTarget(){}public BuildTarget(string spec,int count){this.spec=spec;this.count=count;}}
    public class SearchPolicy
    {
        public string name="candidate";
        public int scale=2,lanes=1,tilesPerLane=5,recipeRule=0;
        public bool upgrade=true,reclaim=true,transport=true,trace=false;
        public bool mixedRecipes=false;
        public double maxSeconds=21600;
        // Ordered cumulative building targets at landing and after each research tier.
        public List<List<BuildTarget>> stages;
        public int[] laneTargets;
        // Optional benchmark action: liquidate capacity above these totals before the Bay.
        public Dictionary<string,int> reclaimAtBay;
    }
}
