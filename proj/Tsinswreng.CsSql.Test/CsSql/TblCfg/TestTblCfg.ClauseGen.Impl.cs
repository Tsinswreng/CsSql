using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊 ClauseGen 各用例。</summary>
	public partial void RegisterClauseGen(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestTblCfg)
			,[typeof(ITable<TestKv>)]
			,[
				nameof(ExtnITable.UpdateClause)
				,nameof(ExtnITable.InsertClause)
				,nameof(ExtnITable.InsertManyClause)
				,nameof(ExtnITable.NumParamClause)
			]
			,nameof(TestTblCfg)
		);
		var R = register.Register;

		R(nameof(ClauseGen_UpdateClause_MultiCols_Exact), ClauseGen_UpdateClause_MultiCols_Exact!);
		R(nameof(ClauseGen_InsertClause_MultiCols_Exact), ClauseGen_InsertClause_MultiCols_Exact!);
		R(nameof(ClauseGen_InsertManyClause_TwoGroups_Exact), ClauseGen_InsertManyClause_TwoGroups_Exact!);
		R(nameof(ClauseGen_NumFieldParam_Name_Suffix), ClauseGen_NumFieldParam_Name_Suffix!);
		R(nameof(ClauseGen_NumParam_ToString_Prefixed), ClauseGen_NumParam_ToString_Prefixed!);
		R(nameof(ClauseGen_NumParamsCount_FromZero), ClauseGen_NumParamsCount_FromZero!);
		R(nameof(ClauseGen_NumParamsRange_Inclusive), ClauseGen_NumParamsRange_Inclusive!);
		R(nameof(ClauseGen_NumParamClause_CorrectCommaSep), ClauseGen_NumParamClause_CorrectCommaSep!);
	}

	/// <summary>UPDATE 的 SET 片段:每列「引用列名 = @參數名」,逗號分隔。</summary>
	public partial async Task<nil> ClauseGen_UpdateClause_MultiCols_Exact(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_Upd");
		var sql = tbl.UpdateClause([nameof(TestKv.KStr), nameof(TestKv.VStr)]);
		T(sql == "\"KStr\" = @KStr, \"VStr\" = @VStr", $"SQL 不匹配.\n{sql}");
		return NIL;
	}

	/// <summary>INSERT 的列與參數片段:「(列1, 列2) VALUES (@參1, @參2)」。</summary>
	public partial async Task<nil> ClauseGen_InsertClause_MultiCols_Exact(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_Ins");
		var sql = tbl.InsertClause([nameof(TestKv.KStr), nameof(TestKv.VStr)]);
		T(sql == "(\"KStr\", \"VStr\") VALUES (@KStr, @VStr)", $"SQL 不匹配.\n{sql}");
		return NIL;
	}

	/// <summary>批量 INSERT 的多組 VALUES:每組參數帶 __N 後綴避免重名。現行輸出在 VALUES 後與組間有雙空格(SQL 合法),按現狀鎖定預期。</summary>
	public partial async Task<nil> ClauseGen_InsertManyClause_TwoGroups_Exact(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_InsMany");
		var sql = tbl.InsertManyClause([nameof(TestKv.KStr)], 2ul);
		T(sql == "(\"KStr\") VALUES  (@KStr__0),  (@KStr__1)", $"SQL 不匹配.\n{sql}");
		return NIL;
	}

	/// <summary>NumFieldParam 生成「字段名__序號」的參數名。</summary>
	public partial async Task<nil> ClauseGen_NumFieldParam_Name_Suffix(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_NumField");
		T(tbl.NumFieldParam(nameof(TestKv.KStr), 3ul).Name == "KStr__3", "參數名應為 KStr__3.");
		return NIL;
	}

	/// <summary>NumParam(N) 生成「_N」參數,ToString 輸出帶 DB 前綴的佔位符。</summary>
	public partial async Task<nil> ClauseGen_NumParam_ToString_Prefixed(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_NumParam");
		T(tbl.NumParam(0ul).ToString() == "@_0", "NumParam(0) 應輸出 @_0.");
		T(tbl.NumParam(5ul).ToString() == "@_5", "NumParam(5) 應輸出 @_5.");
		return NIL;
	}

	/// <summary>NumParams(Cnt) 生成 0..Cnt-1 的參數列表。</summary>
	public partial async Task<nil> ClauseGen_NumParamsCount_FromZero(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_NumParamsCnt");
		var ps = tbl.NumParams(3ul);
		T(ps.Count == 3, "應生成 3 個參數.");
		T(ps[0].ToString() == "@_0" && ps[1].ToString() == "@_1" && ps[2].ToString() == "@_2", "序號應從 0 開始.");
		return NIL;
	}

	/// <summary>NumParams(Start, End) 生成閉區間 Start..End 的參數列表。</summary>
	public partial async Task<nil> ClauseGen_NumParamsRange_Inclusive(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_NumParamsRange");
		var ps = tbl.NumParams(1ul, 3ul);
		T(ps.Count == 3, "閉區間 1..3 應有 3 個參數.");
		T(ps[0].ToString() == "@_1" && ps[2].ToString() == "@_3", "首尾序號應為 1 與 3.");
		return NIL;
	}

	/// <summary>IN 子句參數片段應為「(@0, @1, @2)」:參數間逗號分隔、末尾無多餘逗號。歷史:原實現參數間無逗號、末尾反多一個逗號,已修(2026-08-30)。</summary>
	public partial async Task<nil> ClauseGen_NumParamClause_CorrectCommaSep(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ClauseGen_NumParamClause");
		var sql = tbl.NumParamClause(2ul);
		T(sql == "(@0, @1, @2)", $"SQL 不匹配.\n{sql}");
		return NIL;
	}
}
