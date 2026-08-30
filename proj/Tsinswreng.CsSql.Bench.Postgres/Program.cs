using Npgsql;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Postgres;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsSql.Bench;

namespace Tsinswreng.CsSql.Bench.Postgres;

/// <summary>
/// postgres 性能基準入口。引用 Tsinswreng.CsSql.Bench(lib)並注入 pg 接線,
/// 照 Test.Postgres 的組織方式:入口只負責組裝與執行。
/// 連接目標是 WSL docker 內的 pg(倉庫根 docker-compose.yml:5433→5432)。
/// 基準配置(總行數/輪數/批大小)是入口的聲明性配置,與執行邏輯放在同一入口類。
/// </summary>
internal class Program {
	/// <summary>基準總行數。</summary>
	const int TotalRows = 5000;

	/// <summary>計時輪數,取中位數抗噪聲。</summary>
	const int Rounds = 5;

	/// <summary>pg 批大小掃描(無參數上限顧慮,掃到 1000)。</summary>
	static readonly int[] BatchSizes = [1, 10, 50, 100, 500, 1000];

	/// <summary>連 WSL docker 內的 pg(倉庫根 docker-compose.yml:5433→5432)。</summary>
	const str PgConnStr = "Host=localhost;Port=5433;Database=csql_bench;Username=postgres;Password=CsqlBench";

	/// <summary>
	/// pg 測試域手寫建表 SQL:測試域 DDL 是 sqlite 語法(BLOB/INTEGER 直接入 DDL,
	/// 見 ExtnITable.SqlMkTbl:Col.DbType 非空時原樣使用),pg 需要 bytea/text/bigint。
	/// 列順序與表定義一致,供二進制 COPY 使用。
	/// </summary>
	const str PgCreateTestKvSql = """
		CREATE TABLE IF NOT EXISTS "TestKv" (
			"Id" bytea NOT NULL PRIMARY KEY,
			"Owner" bytea,
			"KStr" text,
			"VStr" text,
			"DelAt" bigint
		);
		""";

	public static async Task Main(string[] Args) {
		await using var Conn = new NpgsqlConnection(PgConnStr);
		await Conn.OpenAsync();

		// 手動接線,不引 DI:cmd maker + tbl mgr + repo 直接 new
		var CmdMkr = new PostgresCmdMkr(new SingletonDbConnGetter(Conn));
		// 用 ITblMgr 類型持有:GetTbl 是接口默認方法(DIM),具體類上不可直接調用
		ITblMgr TblMgr = new PostgresTblMgr();
		TestTblMgrIniter.Init(TblMgr);
		var Repo = new SqlRepo<TestKv, IdTestKv>(TblMgr, CmdMkr, TestDictMapper.Inst);
		var Tbl = TblMgr.GetTbl<TestKv>();
		// 測試域 DDL 是 sqlite 語法,pg 用手寫建表(只建基準用到的 TestKv)
		await ExecSchema(CmdMkr, PgCreateTestKvSql);

		var Raw = new PostgresRawExecutor(Conn, Tbl);
		Console.WriteLine($"== postgres 基準: 總量 {TotalRows} 行, 批大小 [{str.Join(",", BatchSizes)}], 輪數 {Rounds} ==");
		foreach (var BatchSize in BatchSizes) {
			await SqlBenchScenarios.RunPgInsertComparison(Raw, Repo, CmdMkr, TotalRows, BatchSize, Rounds);
		}
		await SqlBenchScenarios.RunRepoReadComparison(Raw, Repo, TotalRows, Rounds);
	}

	/// <summary>執行建表 DDL(不走事務,建表語句多數 DB 不支持事務內執行)。</summary>
	static async Task ExecSchema(ISqlCmdMkr CmdMkr, str SchemaSql) {
		var Cmd = await CmdMkr.MkCmd(null, SchemaSql, default);
		await Cmd.AsyE1d(default).FirstOrDefaultAsync(default);
		await Cmd.DisposeAsync();
	}
}
