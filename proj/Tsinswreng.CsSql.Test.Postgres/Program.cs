using System.Data;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Postgres;
using Tsinswreng.CsSql.Test;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;
using Tsinswreng.Srefl;

namespace Tsinswreng.CsSql.Test.Postgres;

/// <summary>
/// CsSql pg 功能測試入口。引用 CsSql.Test(lib)並注入 pg 的 DI,
/// 照 Test.Sqlite 的組織方式:入口只負責組裝與執行。
/// 連 WSL docker 內的 pg(倉庫根 docker-compose.yml:5433→5432)。
/// </summary>
internal class Program {
	public static IServiceCollection SvcColct = new ServiceCollection();
	public static IServiceProvider SvcProvdr = null!;

	public static async Task Main(string[] args) {
		// 連 WSL docker 內的 pg(見倉庫根 docker-compose.yml:5433→5432)
		const str ConnStr = "Host=localhost;Port=5433;Database=csql_bench;Username=postgres;Password=CsqlBench";
		await using var conn = new NpgsqlConnection(ConnStr);
		await conn.OpenAsync();

		SvcColct
			.AddSingleton<IDbConnection>(conn)
			.AddSingleton<IDbConnMgr>(new SingletonDbConnGetter(conn))
			.AddSingleton<IPropAccessorReg>(TestDictMapper.Inst)
			.AddScoped<ISqlCmdMkr, PostgresCmdMkr>()
			.AddSingleton<ITblMgr>(_ => {
				var mgr = new PostgresTblMgr { };
				TestTblMgrIniter.Init(mgr);
				return mgr;
			})
			.AddScoped<IMkrTxn, PostgresCmdMkr>()
			.AddScoped<ITxnRunner, AdoTxnRunner>()
			.AddScoped<TxnWrapper>()
			.AddRepoScoped<TestKv, IdTestKv>()
			.AddRepoScoped<TestWord, IdTestWord>()
			.AddRepoScoped<TestWordProp, IdTestWordProp>()
			.AddRepoScoped<TestWordLearn, IdTestWordLearn>()
		;

		var mgr = CsSqlTestMgr.Inst;
		SvcProvdr = mgr.InitSvc(SvcColct, sc => sc.BuildServiceProvider());

		// 建表(測試域所有表;pg 兼容 DDL:顯式 "BLOB" 已從測試域移除,由類型映射器決定 bytea)
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
static class ExtnTestPostgresDi {
	public static IServiceCollection AddRepoScoped<TEntity, TId>(this IServiceCollection z)
		where TEntity : class, new() {
		z.AddScoped<IRepo<TEntity, TId>, SqlRepo<TEntity, TId>>();
		return z;
	}
}
