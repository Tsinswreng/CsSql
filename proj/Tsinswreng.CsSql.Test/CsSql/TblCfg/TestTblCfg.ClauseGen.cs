using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊 SQL 片段拼接函數(UpdateClause/InsertClause/InsertManyClause/NumParam 系)的用例。</summary>
	public partial void RegisterClauseGen(ITestNode Node);

	/// <summary>UpdateClause 生成「引用列名 = @參數名」的 SET 片段。</summary>
	public partial Task<nil> ClauseGen_UpdateClause_MultiCols_Exact(obj? O);

	/// <summary>InsertClause 生成「(列) VALUES (@參)」片段。</summary>
	public partial Task<nil> ClauseGen_InsertClause_MultiCols_Exact(obj? O);

	/// <summary>InsertManyClause 生成多組 VALUES,每組參數帶 __N 後綴。</summary>
	public partial Task<nil> ClauseGen_InsertManyClause_TwoGroups_Exact(obj? O);

	/// <summary>NumFieldParam 生成「字段名__序號」的參數名。</summary>
	public partial Task<nil> ClauseGen_NumFieldParam_Name_Suffix(obj? O);

	/// <summary>NumParam(N) 生成「_N」參數,ToString 輸出帶 DB 前綴的佔位符。</summary>
	public partial Task<nil> ClauseGen_NumParam_ToString_Prefixed(obj? O);

	/// <summary>NumParams(Cnt) 生成 0..Cnt-1 的參數列表。</summary>
	public partial Task<nil> ClauseGen_NumParamsCount_FromZero(obj? O);

	/// <summary>NumParams(Start, End) 生成閉區間 Start..End 的參數列表。</summary>
	public partial Task<nil> ClauseGen_NumParamsRange_Inclusive(obj? O);

	/// <summary>NumParamClause 生成 IN 子句參數片段「(@0, @1, @2)」:參數間逗號分隔、末尾無多餘逗號。</summary>
	public partial Task<nil> ClauseGen_NumParamClause_CorrectCommaSep(obj? O);
}
