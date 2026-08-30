using System.Data;
using Npgsql;
using NpgsqlTypes;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Bench.Postgres;

/// <summary>
/// pg 原始 ADO.NET 執行器。直接操作 Npgsql 的 DbCommand / NpgsqlBatch / 二進制 COPY,
/// 不走 CsSql,復現「批量執行層調查」中 pg 的候選策略。
/// 連接由外部持有並與 CsSql Repo 共用;注意 IDbFnCtx.DisposeAsync 會關閉共用連接,
/// 故每次操作前都要確保連接重新打開。
/// </summary>
public sealed class PostgresRawExecutor : IPgBatchExecutor {
	/// <summary>與 CsSql Repo 共用的 pg 連接。</summary>
	readonly NpgsqlConnection Conn;

	/// <summary>TestKv 的表定義,用於列引用/參數名/值轉換,保證與 CsSql 路徑一致。</summary>
	readonly ITable<TestKv> Tbl;

	/// <summary>表的所有列(代碼名,與 SqlRepo.OrdAdd 的 Cols 一致)。</summary>
	readonly IList<str> Cols;

	/// <summary>以共用連接建立執行器。</summary>
	public PostgresRawExecutor(NpgsqlConnection Conn, ITable<TestKv> Tbl) {
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
		// 每條獨立語句、無事務:pg 每條各自提交(autocommit)
		await using var Cmd = Conn.CreateCommand();
		Cmd.CommandText = MkInsertSql(1);
		foreach (var Row in Rows) {
			Cmd.Parameters.Clear();
			BindRow(Cmd, Row);
			await Cmd.ExecuteNonQueryAsync();
		}
	}

	public async Task InsertTxnLoop(IList<TestKv> Rows) {
		await EnsureOpen();
		await using var Txn = Conn.BeginTransaction();
		await using var Cmd = Conn.CreateCommand();
		Cmd.Transaction = Txn;
		Cmd.CommandText = MkInsertSql(1);
		foreach (var Row in Rows) {
			Cmd.Parameters.Clear();
			BindRow(Cmd, Row);
			await Cmd.ExecuteNonQueryAsync();
		}
		await Txn.CommitAsync();
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
		await Txn.CommitAsync();
	}

	public async Task InsertNpgsqlBatch(IList<TestKv> Rows, int BatchSize) {
		await EnsureOpen();
		await using var Txn = Conn.BeginTransaction();
		// 每批 BatchSize 條單行 INSERT 打包進一個 NpgsqlBatch,一次往返
		for (var Start = 0; Start < Rows.Count; Start += BatchSize) {
			var Count = Math.Min(BatchSize, Rows.Count - Start);
			await using var Batch = new NpgsqlBatch(Conn) {
				Transaction = Txn,
			};
			for (var J = 0; J < Count; J++) {
				var BatchCmd = new NpgsqlBatchCommand(MkInsertSql(1));
				BindRow(BatchCmd, Rows[Start + J]);
				Batch.BatchCommands.Add(BatchCmd);
			}
			await Batch.ExecuteNonQueryAsync();
		}
		await Txn.CommitAsync();
	}

	public async Task InsertCopy(IList<TestKv> Rows) {
		await EnsureOpen();
		// COPY 本身是原子的,不需要外層事務;二進制 COPY 列順序必須與表定義一致,一次性流式寫入
		var ColNames = str.Join(", ", Cols.Select(Tbl.QtCol));
		var CopySql = $"COPY {Tbl.Qt(Tbl.DbTblName)} ({ColNames}) FROM STDIN (FORMAT BINARY)";
		await using var Writer = await Conn.BeginBinaryImportAsync(CopySql);
		foreach (var Row in Rows) {
			Writer.StartRow();
			var DbDict = Tbl.ToDbDict(Tbl.EntityToCodeDict(Row));
			foreach (var Col in Cols) {
				WriteColValue(Writer, DbDict[Tbl.DbColName(Col)]);
			}
		}
		Writer.Complete();
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

	/// <summary>把一行實體綁定為 NpgsqlCommand 參數(值轉換走 ITable.ToDbDict,與 CsSql 路徑一致)。
	/// 參數名用 ToString()(帶 @ 前綴),與 BaseSqlCmd.RawArgs 的 ToResolvedArg 一致。</summary>
	void BindRow(NpgsqlCommand Cmd, TestKv Row, u64 Idx = 0) {
		var DbDict = Tbl.ToDbDict(Tbl.EntityToCodeDict(Row));
		foreach (var Col in Cols) {
			var Val = DbDict[Tbl.DbColName(Col)];
			Cmd.Parameters.AddWithValue(Tbl.NumFieldParam(Col, Idx).ToString(), Val ?? DBNull.Value);
		}
	}

	/// <summary>把一行實體綁定為 NpgsqlBatchCommand 參數(單行語句,行號固定 0)。</summary>
	void BindRow(NpgsqlBatchCommand Cmd, TestKv Row) {
		var DbDict = Tbl.ToDbDict(Tbl.EntityToCodeDict(Row));
		foreach (var Col in Cols) {
			var Val = DbDict[Tbl.DbColName(Col)];
			Cmd.Parameters.AddWithValue(Tbl.NumFieldParam(Col, 0).ToString(), Val ?? DBNull.Value);
		}
	}

	/// <summary>
	/// 按值類型寫入 COPY 二進制流。測試域的列類型只有 bytea / text / bigint(強類型 Id 存 BLOB),
	/// null 寫空值;出現其他類型直接報錯,避免靜默寫錯格式。
	/// </summary>
	static void WriteColValue(NpgsqlBinaryImporter Writer, obj? Val) {
		switch (Val) {
			case byte[] Bytes:
				Writer.Write(Bytes, NpgsqlDbType.Bytea);
				break;
			case str Text:
				Writer.Write(Text, NpgsqlDbType.Text);
				break;
			case long L:
				Writer.Write(L, NpgsqlDbType.Bigint);
				break;
			case null:
				Writer.WriteNull();
				break;
			default:
				throw new NotSupportedException($"COPY 不支持的列值類型: {Val.GetType()}");
		}
	}
}
