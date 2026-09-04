namespace Tsinswreng.CsSql;

/// SqlMkr.Build() 的產物：一段拼好的完整 SQL + 參數表，等待被執行端消費。
///
/// 命名說明：「EtArg」= Execution Argument——它是構造端交給執行端的入參材料，
/// 不是 statement（statement 是 prepare 之後才配叫的名字）：
/// 執行端三件套（Run / RunQuery / RunQueryAligned）拿它去 Prepare 出真正可執行的命令。
///
/// 執行方式（配合現有執行層，三件套落在 SqlCmdMkr）：
/// ```csharp
/// var Cmd = await SqlCmdMkr.Prepare(Ctx, EtArg.Sql, Ct);
/// Ctx.AddToDispose(Cmd);
/// await Cmd.RawArgs(EtArg.Args.ToDict()).AsyE1d(Ct)...;
/// ```
///
/// 只有兩個成員，沒有「份數」：同構批量拼出 N 條語句後，
/// 執行端按結果集數逐槽讀取（AsyE1dWithNull），天然位置對齊，不需要預先聲明份數。
public interface ISqlEtArg{
	/// 完整 SQL 文本：可含多條 ';' 拼接的同構批量語句。
	public str Sql{get;}

	/// 參數表：已按列做 Upper→Raw、名字全程唯一（批量時自動帶對序後綴如 col__0）。
	public IArgDict Args{get;}
}