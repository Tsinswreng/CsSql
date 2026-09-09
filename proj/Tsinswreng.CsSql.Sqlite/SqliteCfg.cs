namespace Tsinswreng.CsSql.Sqlite;

using System.Data;
using Tsinswreng.CsSql;

/// <summary>
/// sqlite 接入配置:連接與表管理器。
/// 「使用方只提供業務參數、庫註冊固定配方」的接入形態,由 `AddCsSqlSqlite` 消費。
/// </summary>
public sealed class SqliteCfg{
	/// <summary>已打開的 sqlite 連接(單例復用;接入方自己管理打開/關閉)。</summary>
	public IDbConnection Connection{get;set;}

	/// <summary>配好全部表的表管理器(接入方自己構建,如繼承 SqliteTblMgr 並灌表)。</summary>
	public ITblMgr TblMgr{get;set;}
}