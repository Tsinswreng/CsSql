using System.Data;
using Microsoft.Data.Sqlite;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Bench.Sqlite;

/// <summary>
/// sqlite 原始 ADO.NET 執行器。直接操作 Microsoft.Data.Sqlite 的 DbCommand,不走 CsSql,
/// 復現「批量執行層調查」中 sqlite 的候選策略。
/// 連接由外部持有並與 CsSql Repo 共用;注意 IDbFnCtx.DisposeAsync 會關閉共用連接,
/// 故每次操作前都要確保連接重新打開(文件庫重開數據仍在)。
/// </summary>
public sealed class SqliteRawExecutor : IRawDbExecutor {
	/// <summary>與 CsSql Repo 共用的 sqlite 連接。</summary>
	readonly SqliteConnection Conn;

	/// <summary>TestKv 的表定義,用於列引用/參數名/值轉換,保證與 CsSql 路徑一致。</summary>
	readonly ITable<TestKv> Tbl;

	/// <summary>表的所有列(代碼名,與 SqlRepo.OrdAdd 的 Cols 一致)。</summary>
	readonly IList<str> Cols;

	/// <summary>以共用連接建立執行器。</summary>
	public SqliteRawExecutor(SqliteConnection Conn, ITable<TestKv> Tbl) {
		this.Conn = Conn;
		this.Tbl = Tbl;
		Cols = Tbl.Columns.Keys.ToList();
	}

	/// <summary>確保連接打開;被 IDbFnCtx.DisposeAsync 關閉後重新打開。</summary>
	async Task EnsureOpen() {
		if (Conn.State != ConnectionState.Open) {
			await Conn.OpenAsync();
		}
	}

	/// <summary>釋放執行器。連接由外部持有,這裡不關閉。</summary>
	public ValueTask DisposeAsync() {
		return ValueTask.CompletedTask;
	}

	public async Task ClearTable(str TableName) {
		await EnsureOpen();
		await using var Cmd = Conn.CreateCommand();
		Cmd.CommandText = $"DELETE FROM {Tbl.Qt(TableName)}";
		await Cmd.ExecuteNonQueryAsync();
	}

	public async Task InsertSingleNoTxn(IList<TestKv> Rows) {
		await EnsureOpen();
		// 每條獨立語句、無事務:sqlite 每條各自提交(autocommit)
		await using var Cmd = Conn.CreateCommand();
		Cmd.CommandText = MkInsertSql(1);
		foreach (var Row in Rows) {
			// 每行重新綁定參數:同一命令反覆 Add 同名參數會重複/衝突
			Cmd.Parameters.Clear();
			BindRow(Cmd, Row, 0);
			await Cmd.ExecuteNonQueryAsync();
		}
	}

	public async Task InsertTxnLoop(IList<TestKv> Rows) {
		await EnsureOpen();
		await using var Txn = Conn.BeginTransaction();
		await using var Cmd = Conn.CreateCommand();
		Cmd.Transaction = Txn;
		Cmd.CommandText = MkInsertSql(1);
		// 預先建立參數對象、Prepare 一次,循環中只改 Value——這才是「prepare 復用」的形態
		var Params = BindRow(Cmd, Rows[0], 0);
		Cmd.Prepare();
		foreach (var Row in Rows) {
			RebindParams(Params, Row);
			await Cmd.ExecuteNonQueryAsync();
		}
		Txn.Commit();
	}

	public async Task InsertMultiValues(IList<TestKv> Rows, int BatchSize) {
		await EnsureOpen();
		await using var Txn = Conn.BeginTransaction();
		await using var Cmd = Conn.CreateCommand();
		Cmd.Transaction = Txn;
		// 分批:每批 BatchSize 行拼一條多行 VALUES 語句
		for (var Start = 0; Start < Rows.Count; Start += BatchSize) {
			var Count = Math.Min(BatchSize, Rows.Count - Start);
			Cmd.CommandText = MkInsertSql(Count);
			Cmd.Parameters.Clear();
			for (var J = 0; J < Count; J++) {
				BindRow(Cmd, Rows[Start + J], (u64)J);
			}
			await Cmd.ExecuteNonQueryAsync();
		}
		Txn.Commit();
	}

	public async Task<IDbFnCtx> NewCtx() {
		await EnsureOpen();
		// 與原始執行器共用同一連接;注意釋放此 Ctx 會關閉連接(見 IDbFnCtx.DisposeAsync)
		return new DbFnCtx {
			DbConn = Conn,
		};
	}

	/// <summary>
	/// 生成 INSERT 語句:單行或 BatchSize 行的多行 VALUES。
	/// 參數名 `{列名}__{行號}`,與 SqlRepo.OrdAdd 的拼法一致。
	/// </summary>
	str MkInsertSql(int BatchSize) {
		var Fields = str.Join(", ", Cols.Select(Tbl.QtCol));
		var Values = str.Join(", ", Enumerable.Range(0, BatchSize).Select(I =>
			"(" + str.Join(", ", Cols.Select(C => Tbl.NumFieldParam(C, (u64)I).ToString())) + ")"
		));
		return $"INSERT INTO {Tbl.Qt(Tbl.DbTblName)} ({Fields}) VALUES {Values}";
	}

	/// <summary>
	/// 把一行實體綁定為命令參數(值轉換走 ITable.ToDbDict,與 CsSql 路徑一致)。
	/// 參數名用 ToString()(帶 @ 前綴):Microsoft.Data.Sqlite 按「SQL 佔位符 ↔ 參數名」精確匹配,
	/// 不帶前綴的裸名匹配不上(與 BaseSqlCmd.RawArgs 的 ToResolvedArg 一致)。
	/// 返回建立的參數列表,供 prepare 復用場景反覆改值。
	/// </summary>
	SqliteParameter[] BindRow(SqliteCommand Cmd, TestKv Row, u64 Idx) {
		var DbDict = Tbl.ToDbDict(Tbl.EntityToCodeDict(Row));
		var Params = new SqliteParameter[Cols.Count];
		for (var I = 0; I < Cols.Count; I++) {
			var Val = DbDict[Tbl.DbColName(Cols[I])];
			var P = new SqliteParameter(Tbl.NumFieldParam(Cols[I], Idx).ToString(), Val ?? DBNull.Value);
			Cmd.Parameters.Add(P);
			Params[I] = P;
		}
		return Params;
	}

	/// <summary>只改參數值不動命令文本/參數集合,保持 prepare 的語句緩存有效。</summary>
	void RebindParams(SqliteParameter[] Params, TestKv Row) {
		var DbDict = Tbl.ToDbDict(Tbl.EntityToCodeDict(Row));
		for (var I = 0; I < Cols.Count; I++) {
			Params[I].Value = DbDict[Tbl.DbColName(Cols[I])] ?? DBNull.Value;
		}
	}
}
