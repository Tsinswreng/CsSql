namespace Tsinswreng.CsSql;

/// SqlMkr.Build() 的產物：一條命令可直接執行的完整 SQL + 參數表。
///
/// 執行方式（配合現有執行層）：
/// ```csharp
/// var Cmd = await SqlCmdMkr.Prepare(Ctx, Stmt.Sql, Ct);
/// Ctx.AddToDispose(Cmd);
/// await Cmd.RawArgs(Stmt.Args.ToDict()).AsyE1d(Ct)...;
/// ```
public partial class SqlStmt{
	/// 完整 SQL 文本：可含多條 ';' 拼接的同構批量語句（TmplCnt > 1 時）。
	public str Sql{get;set;}

	/// 參數表：已按列做 Upper→Raw、名字全程唯一（批量時自動帶對序後綴如 col__0）。
	public IArgDict Args{get;set;}

	/// 份數：1 = 單語句；N = 同構批量（N 份模板語句，執行層據此做位置對齊回讀）。
	/// 配合 `IResultReader.AsyE1dWithNull`：逐結果集取首行、空補 null、與入參一一對應。
	public u64 TmplCnt{get;set;} = 1;
}