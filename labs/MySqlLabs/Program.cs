using System.Diagnostics;
using System.Runtime.InteropServices;
using MySqlConnector;
var settings=new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("LAB_DB") ?? throw new Exception("LAB_DB required"));
if(settings.Database!="clinicflow_labs")throw new Exception("Labs require database clinicflow_labs");
var connection=settings.ConnectionString;settings.Database="";
await using(var admin=new MySqlConnection(settings.ConnectionString)){await admin.OpenAsync();await new MySqlCommand("CREATE DATABASE IF NOT EXISTS clinicflow_labs",admin).ExecuteNonQueryAsync();}
await using var db=new MySqlConnection(connection);await db.OpenAsync();
Console.WriteLine($"UTC={DateTime.UtcNow:O}; OS={RuntimeInformation.OSDescription}; arch={RuntimeInformation.ProcessArchitecture}; .NET={Environment.Version}; CPUs={Environment.ProcessorCount}; MySQL={db.ServerVersion}");
async Task Exec(string sql){await new MySqlCommand(sql,db).ExecuteNonQueryAsync();}
if(args.FirstOrDefault()=="L1")
{
 var fixedMode=args.Contains("--fixed");
 await Exec("DROP TABLE IF EXISTS lost_update; CREATE TABLE lost_update(id INT PRIMARY KEY,note VARCHAR(40),version INT NOT NULL); INSERT INTO lost_update VALUES(1,'original',1)");
 var bothRead=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var firstWritten=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int readers=0;
 async Task<int> Client(string note,bool first)
 {
  await using var conn=new MySqlConnection(connection);await conn.OpenAsync();var version=Convert.ToInt32(await new MySqlCommand("SELECT version FROM lost_update WHERE id=1",conn).ExecuteScalarAsync());
  if(Interlocked.Increment(ref readers)==2)bothRead.SetResult();await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(10));
  if(!first)await firstWritten.Task.WaitAsync(TimeSpan.FromSeconds(10));
  using var update=new MySqlCommand("UPDATE lost_update SET note=@note,version=version+1 WHERE id=1"+(fixedMode?" AND version=@version":""),conn);update.Parameters.AddWithValue("@note",note);update.Parameters.AddWithValue("@version",version);var rows=await update.ExecuteNonQueryAsync();if(first)firstWritten.SetResult();return rows;
 }
 var results=await Task.WhenAll(Client("client-A",true),Client("client-B",false));
 using var result=await new MySqlCommand("SELECT note,version FROM lost_update",db).ExecuteReaderAsync();await result.ReadAsync();Console.WriteLine($"L1 fixed={fixedMode}; both read v1; affected=[{string.Join(',',results)}]; final={result.GetString(0)},v{result.GetInt32(1)}");
 if(results.Sum()!=(fixedMode?1:2))throw new Exception("Unexpected race result");
}
else if(args.FirstOrDefault()=="L3")
{
 var count=args.Length>1?int.Parse(args[1]):100000;if(count<1000||count>1000000)throw new Exception("Use 1000..1000000 rows");
 await Exec("DROP TABLE IF EXISTS slow_appointments; CREATE TABLE slow_appointments(id INT PRIMARY KEY,resource_id INT NOT NULL,start_utc DATETIME NOT NULL,status VARCHAR(20) NOT NULL,note VARCHAR(256) NOT NULL)");
 // Deterministic synthetic set. Six digit tables generate at most one million rows.
 const string digit="(SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9)";
 var numbers=$"SELECT a.n+10*b.n+100*c.n+1000*d.n+10000*e.n+100000*f.n n FROM {digit} a CROSS JOIN {digit} b CROSS JOIN {digit} c CROSS JOIN {digit} d CROSS JOIN {digit} e CROSS JOIN {digit} f";
 using(var insert=new MySqlCommand($"INSERT INTO slow_appointments SELECT n+1,MOD(n,100),DATE_ADD('2030-01-01',INTERVAL n MINUTE),'Pending',REPEAT('x',128) FROM ({numbers}) seq WHERE n<@count",db)){insert.Parameters.AddWithValue("@count",count);await insert.ExecuteNonQueryAsync();}
 const string query="SELECT id,start_utc FROM slow_appointments WHERE resource_id=42 AND start_utc>='2030-01-01' ORDER BY start_utc,id LIMIT 50 OFFSET 5";
 async Task<(List<int> ids,double median)> Measure(string label)
 {
  using(var plan=await new MySqlCommand("EXPLAIN ANALYZE "+query,db).ExecuteReaderAsync()){while(await plan.ReadAsync())Console.WriteLine(label+" PLAN: "+plan.GetString(0));}
  var times=new List<double>();var ids=new List<int>();
  for(var round=0;round<5;round++){ids.Clear();var watch=Stopwatch.StartNew();using var reader=await new MySqlCommand(query,db).ExecuteReaderAsync();while(await reader.ReadAsync())ids.Add(reader.GetInt32(0));times.Add(watch.Elapsed.TotalMilliseconds);}
  times.Sort();Console.WriteLine($"{label}: rows={count}; resource=42; offset=5; limit=50; median of 5={times[2]:F3}ms; resultCount={ids.Count}");return(ids.ToList(),times[2]);
 }
 var before=await Measure("before");await Exec("CREATE INDEX ix_resource_time_id ON slow_appointments(resource_id,start_utc,id)");var after=await Measure("after");if(!before.ids.SequenceEqual(after.ids))throw new Exception("Result/pagination semantics changed");Console.WriteLine("L3 PASS identical ordered result IDs before/after. Laptop experiment only; timings are not production claims.");
}
else throw new Exception("Choose L1 [--fixed] or L3 [row count]");
