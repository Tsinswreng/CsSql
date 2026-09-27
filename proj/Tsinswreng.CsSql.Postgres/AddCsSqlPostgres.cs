namespace Tsinswreng.CsSql.Postgres.Di;

using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsinswreng.CsSql;

/// <summary>
/// PostgreSQL 接入 DI 擴展:把按 DB 類型固定的「基礎設施配方」一次註冊完。
/// 使用方只需提供數據源與表管理器,其餘(IDbConnMgr/ISqlCmdMkr/IMkrTxn/ITxnRunner/IMkrDbFnCtx/TxnWrapper)由本方法註冊;
/// 遷移清單、每實體 Repo、映射註冊是業務內容,仍由使用方自己註冊。
/// </summary>
public static class CsSqlPostgresDiExtn{
	/// <summary>註冊 PostgreSQL 接入所需全部固定服務,返回同一集合以便鏈式繼續註冊業務服務。</summary>
	public static IServiceCollection AddCsSqlPostgres(
		this IServiceCollection z
		,PostgresCfg Cfg
	){
		if(Cfg.DataSource is null){
			throw new ArgumentNullException(nameof(Cfg.DataSource));
		}
		if(Cfg.TblMgr is null){
			throw new ArgumentNullException(nameof(Cfg.TblMgr));
		}

		// 連接管理器:池模式(每次 OpenConnectionAsync 拿新連接)
		z.AddSingleton<IDbConnMgr>(new PostgresConnPool(Cfg.DataSource));
		// 命令製造器與事務製造器共用同一實現
		z.AddScoped<ISqlCmdMkr, PostgresCmdMkr>();
		z.AddScoped<IMkrTxn, PostgresCmdMkr>();
		// 事務執行器與「Fn 閉包 + 事務包裝」路徑(舊範式仍用)
		z.AddScoped<ITxnRunner, AdoTxnRunner>();
		z.AddScoped<IMkrDbFnCtx, MkrDbFnCtx>();
		z.AddScoped<TxnWrapper>();
		// 表管理器:使用方配好全部表後傳入
		z.AddSingleton<ITblMgr>(Cfg.TblMgr);
		return z;
	}
}
