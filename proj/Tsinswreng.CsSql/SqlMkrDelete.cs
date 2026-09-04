using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

namespace Tsinswreng.CsSql;

/// DELETE 構造器（T.SqlMkr().Delete() 的鏈式返回類型）。
///
/// 三種語義：
/// - 硬刪：AndIn/HardIn → `DELETE FROM t WHERE CodeCol IN (...)` 一條語句；
/// - 軟刪：SoftIn → `UPDATE t SET {SoftDelCol} = @v WHERE CodeCol IN (...)`（表需配置 SoftDelCol）；
/// - 逃逸：And → 多表子查詢 DELETE 等手拼場景。
public partial class SqlMkrDelete{
	/// 綁定的表。
	public ITable Tbl{get;set;}

	/// 已拼 SQL 段（含佔位符）緩衝。
	public IList<obj> Segs{get;set;} = [];

	/// 共享參數表（raw 值、名字全程唯一）。
	public IArgDict Args{get;set;} = ArgDict.Mk();

	/// 硬刪：按 CodeCol IN 一批刪除（一條語句，無序）。
	public partial SqlMkrDelete AndIn<T>(str CodeCol, IList<T> Uppers);

	/// 硬刪（主鍵一批，語義化別名）：`DELETE ... WHERE IdCodeCol IN (...)`
	/// 對標舊 HardDelInId/OrdHardDelById，參數自動 Upper→Raw。
	/// 泛型 T 使 IList<IdWord> 等值類型 Id 不需要手動轉 obj?。
	public partial SqlMkrDelete HardIn<T>(str CodeCol, IList<T> Ids);

	/// 軟刪：`UPDATE t SET {SoftDelCol} = FnDelete(null) WHERE CodeCol IN (...)`
	/// 對標舊 SoftDelInId/OrdSoftDelById；表未配置 SoftDelCol 時 Build 報錯。
	public partial SqlMkrDelete SoftIn<T>(str CodeCol, IList<T> Ids);

	/// AND 原始 SQL 片段（逃逸通道：多表 DELETE、子查詢條件等）。
	public partial SqlMkrDelete And(str RawSql);

	/// 拼出 ISqlEtArg。
	public partial ISqlEtArg Build();
}