using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Bench;

/// <summary>
/// 原始 ADO.NET 執行器:直接操作 DbCommand,不走 CsSql。
/// sqlite/pg 各自實現,抹平語法差異(參數佔位符、多行 VALUES 語法)。
/// 三種插入寫法對應「批量執行層調查」中 sqlite/pg 共用的候選策略,
/// 用於為執行引擎改造出對比數據,不改庫。
/// </summary>
public interface IRawDbExecutor : IAsyncDisposable {
	/// <summary>清空表(不走 CsSql,直接 DELETE)。</summary>
	Task ClearTable(str TableName);

	/// <summary>單條循環、無事務——基線。每條獨立語句,各自提交。</summary>
	Task InsertSingleNoTxn(IList<TestKv> Rows);

	/// <summary>事務內循環:prepare 一次、反覆換參數復用。每條獨立語句共用一個事務。</summary>
	Task InsertTxnLoop(IList<TestKv> Rows);

	/// <summary>多行 VALUES:每批 BatchSize 行拼成一條語句,事務內執行。</summary>
	Task InsertMultiValues(IList<TestKv> Rows, int BatchSize);

	/// <summary>開一個 CsSql IDbFnCtx(供 IRepo API 用),連接與原始執行器共用。</summary>
	Task<IDbFnCtx> NewCtx();
}

/// <summary>
/// pg 專用的額外執行通道:IRawDbExecutor 三種寫法之外的候選策略。
/// NpgsqlBatch:多條獨立小語句打包成一次往返;COPY:原生二進制批量通道。
/// </summary>
public interface IPgBatchExecutor : IRawDbExecutor {
	/// <summary>NpgsqlBatch:每批 BatchSize 條單行 INSERT 打包成一次執行。</summary>
	Task InsertNpgsqlBatch(IList<TestKv> Rows, int BatchSize);

	/// <summary>COPY:原生二進制批量導入(NpgsqlBinaryImporter),一次性寫入。</summary>
	Task InsertCopy(IList<TestKv> Rows);
}
