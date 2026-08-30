using Microsoft.Data.Sqlite;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Sqlite;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsSql.Bench;

namespace Tsinswreng.CsSql.Bench.Sqlite;

/// <summary>
/// sqlite 性能基準入口。引用 Tsinswreng.CsSql.Bench(lib)並注入 sqlite 接線,
/// 照 Test.Sqlite 的組織方式:入口只負責組裝與執行。
/// 基準配置(總行數/輪數/批大小)是入口的聲明性配置,與執行邏輯放在同一入口類。
/// </summary>
internal class Program {
	/// <summary>基準總行數(單條無事務每行一次 fsync,太大會跑很久)。</summary>
	const int TotalRows = 2000;

	/// <summary>計時輪數,取中位數抗噪聲。</summary>
	const int Rounds = 5;

	/// <summary>
	/// 批大小掃描:TestKv 5 列 → sqlite 999 參數上限 ≈ 199 行/批。
	/// </summary>
	static readonly int[] BatchSizes = [1, 10, 50, 100, 199];

	public static async Task Main(string[] Args) {
		// 工作區文件庫(照 Test.Sqlite:文件庫在連接關閉重開後數據仍在;:memory: 會被 Ctx.Dispose 清空)
		var WorkDir = Path.Combine(AppContext.BaseDirectory, "benchdata");
		Directory.CreateDirectory(WorkDir);
		var DbPath = Path.Combine(WorkDir, "bench_" + Guid.NewGuid().ToString("N") + ".db");
		try {
			// Pooling=False:關閉連接即釋放文件句柄,否則連接池會一直佔住 .db 文件、finally 刪不掉
			await using (var Conn = new SqliteConnection($"Data Source={DbPath};Pooling=False")) {
				await Conn.OpenAsync();
				// 手動接線,不引 DI:cmd maker + tbl mgr + repo 直接 new
				var CmdMkr = new SqliteCmdMkr(new SingletonDbConnGetter(Conn));
				// 用 ITblMgr 類型持有:GetTbl 是接口默認方法(DIM),具體類上不可直接調用
				ITblMgr TblMgr = new SqliteTblMgr();
				TestTblMgrIniter.Init(TblMgr);
				var Repo = new SqlRepo<TestKv, IdTestKv>(TblMgr, CmdMkr, TestDictMapper.Inst);
				var Tbl = TblMgr.GetTbl<TestKv>();
				// 建表(測試域所有表)
				await ExecSchema(CmdMkr, TblMgr.SqlMkSchema());

				var Raw = new SqliteRawExecutor(Conn, Tbl);
				Console.WriteLine($"== sqlite 基準: 總量 {TotalRows} 行, 批大小 [{str.Join(",", BatchSizes)}], 輪數 {Rounds} ==");
				foreach (var BatchSize in BatchSizes) {
					await SqlBenchScenarios.RunInsertComparison(Raw, Repo, CmdMkr, TotalRows, BatchSize, Rounds);
				}
				await SqlBenchScenarios.RunRepoReadComparison(Raw, Repo, TotalRows, Rounds);
			}
		} finally {
			// 釋放文件句柄後再刪:連接關閉後句柄釋放可能異步完成/被 GC 延後/被殺毒短暫佔用,故強制回收 + 重試
			SqliteConnection.ClearAllPools();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			for (var I = 0; I < 30; I++) {
				try {
					File.Delete(DbPath);
					break;
				} catch (IOException) when (I < 29) {
					await Task.Delay(200);
				}
			}
		}
	}

	/// <summary>執行建表 DDL(不走事務,建表語句多數 DB 不支持事務內執行)。</summary>
	static async Task ExecSchema(ISqlCmdMkr CmdMkr, str SchemaSql) {
		var Cmd = await CmdMkr.MkCmd(null, SchemaSql, default);
		await Cmd.AsyE1d(default).FirstOrDefaultAsync(default);
		await Cmd.DisposeAsync();
	}
}
