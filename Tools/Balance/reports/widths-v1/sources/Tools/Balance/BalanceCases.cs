using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
namespace FactoryBalance {
 public static class BalanceCases {
  public static void Run(string cases,float step,int workers){
   var rows=JsonSerializer.Deserialize<string[][]>(File.ReadAllText(cases));
   Parallel.ForEach(rows,new ParallelOptions{MaxDegreeOfParallelism=workers},row=>{
    if(File.Exists(row[2]))throw new Exception("Refusing to overwrite replay: "+row[2]);
    BatchSearch.Replay(row[0],row[1],row[2],step);
    using(var doc=JsonDocument.Parse(File.ReadAllText(row[2]))){Console.WriteLine(Path.GetFileName(row[2])+" "+doc.RootElement.GetProperty("success")+" "+(doc.RootElement.GetProperty("seconds").GetDouble()/60).ToString("F2"));}
   });
  }
 }
}
