using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
namespace FactoryBalance {
 public static class StarterCalibration {
  public static void Run(string file,string output,int workers){
   var cases=JsonSerializer.Deserialize<string[][]>(File.ReadAllText(file));var results=new object[cases.Length];int done=0;object gate=new object();
   Parallel.For(0,cases.Length,new ParallelOptions{MaxDegreeOfParallelism=workers},i=>{
    var row=cases[i];var p=new FactoryPlanner(Runner.Load(row[0]),new SearchPolicy{reclaim=false,trace=false,mixedRecipes=true,maxSeconds=172800},.5f);
    bool replaceAll=!(row.Length>4&&row[4]=="fixedEndpoint");
    var result=JsonSerializer.SerializeToElement(p.StarterPath(row[1],false,replaceAll,row.Length>3&&row[3]=="freeLogistics"),Runner.Json);
    results[i]=new{id=row[2],allocation=row[1],path=row[1].Length+1,result};
    lock(gate){done++;if(done%25==0||done==cases.Length)Console.WriteLine(done+" / "+cases.Length+" calculated");}
   });
   File.WriteAllText(output,JsonSerializer.Serialize(results,Runner.Json));
  }
 }
}
