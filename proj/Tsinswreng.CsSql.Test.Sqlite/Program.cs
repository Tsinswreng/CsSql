using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Sqlite;
using Tsinswreng.CsSql.Test;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;
using Tsinswreng.Srefl;

namespace Tsinswreng.CsSql.Test.Sqlite;

/// <summary>
/// CsSql sqlite 測試入口。引用 CsSql.Test(lib)並注入 sqlite 的 DI,
/// 照 Ngan.Dict.Windows.Test 的組織方式:入口只負責組裝與執行。
/// </summary>
internal class Program {
	public static IServiceCollection SvcColct = new ServiceCollection();
	public static IServiceProvider SvcProvdr = null!;

	public static async Task Main(string[] args) {
		// sqlite 文件庫(工作區內):測試後刪除。照 Ngan.Dict 的做法(文件庫在連接關閉重開後數據仍在,
		// 而 :memory: 庫在每個用例的 Ctx.DisposeAsync 關閉連接時會整個清空)。
		var workDir = Path.Combine(AppContext.BaseDirectory, "testdata");
		Directory.CreateDirectory(workDir);
		var dbPath = Path.Combine(workDir, "csql_test_" + Guid.NewGuid().ToString("N") + ".db");
		try {
			await Run(dbPath);
		} finally {
			try {
				File.Delete(dbPath);
			} catch {
				// 測試庫刪除失敗不影響測試結果
			}
		}
	}

	static async Task Run(str dbPath) {
		var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		SvcColct
			.AddSingleton<IDbConnection>(conn)
			.AddSingleton<IDbConnMgr>(new SingletonDbConnGetter(conn))
			.AddSingleton<IPropAccessorReg>(TestDictMapper.Inst)
			.AddScoped<ISqlCmdMkr, SqliteCmdMkr>()
			.AddSingleton<ITblMgr>(_ => {
				var mgr = new SqliteTblMgr { };
				TestTblMgrIniter.Init(mgr);
				return mgr;
			})
			.AddScoped<IMkrTxn, SqliteCmdMkr>()
			.AddScoped<ITxnRunner, AdoTxnRunner>()
			.AddScoped<TxnWrapper>()
			.AddRepoScoped<TestKv, IdTestKv>()
			.AddRepoScoped<TestWord, IdTestWord>()
			.AddRepoScoped<TestWordProp, IdTestWordProp>()
			.AddRepoScoped<TestWordLearn, IdTestWordLearn>()
		;

		var mgr = CsSqlTestMgr.Inst;
		SvcProvdr = mgr.InitSvc(SvcColct, sc => sc.BuildServiceProvider());

		// 建表(文件庫建一次,所有測試共用)。
		var cmdMkr = SvcProvdr.GetRequiredService<ISqlCmdMkr>();
		var tblMgr = SvcProvdr.GetRequiredService<ITblMgr>();
		var schemaSql = tblMgr.SqlMkSchema();
		var cmd = await cmdMkr.MkCmd(null, schemaSql, default);
		await cmd.AsyE1d(default).FirstOrDefaultAsync(default);
		await cmd.DisposeAsync();

		ITestExecutor executor = new TreeTestExecutor();
		await executor.RunEtPrint(mgr.TestNode);
	}
}

/// <summary>測試入口的 IRepo 註冊助手(照 Ngan.Dict 的 AddRepoScoped)。</summary>
static class ExtnTestSqliteDi {
	public static IServiceCollection AddRepoScoped<TEntity, TId>(this IServiceCollection z)
		where TEntity : class, new() {
		z.AddScoped<IRepo<TEntity, TId>, SqlRepo<TEntity, TId>>();
		return z;
	}
}
