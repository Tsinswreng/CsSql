namespace Tsinswreng.CsSql.Postgres;

using Npgsql;
using Tsinswreng.CsSql;

/// <summary>
/// PostgreSQL 接入配置:數據源與表管理器。
/// 「使用方只提供業務參數、庫註冊固定配方」的接入形態,由 `AddCsSqlPostgres` 消費。
/// </summary>
public sealed class PostgresCfg{
	/// <summary>連接池數據源(接入方自己構建,如 NpgsqlDataSourceBuilder.Build())。</summary>
	public NpgsqlDataSource DataSource{get;set;}

	/// <summary>配好全部表的表管理器(接入方自己構建)。</summary>
	public ITblMgr TblMgr{get;set;}
}