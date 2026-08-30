using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊 SoftDel 各用例。</summary>
	public partial void RegisterSoftDel(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestTblCfg)
			,[typeof(ITable<TestKv>)]
			,[nameof(ExtnITable.SqlIsNonDel), nameof(ExtnITable.AndSqlIsNonDel)]
			,nameof(TestTblCfg)
		);
		var R = register.Register;

		R(nameof(SoftDel_SqlIsNonDel_WithSoftDelCol_Exact), SoftDel_SqlIsNonDel_WithSoftDelCol_Exact!);
		R(nameof(SoftDel_AndSqlIsNonDel_WithSoftDelCol_Exact), SoftDel_AndSqlIsNonDel_WithSoftDelCol_Exact!);
		R(nameof(SoftDel_AndSqlIsNonDel_NoSoftDelCol_Empty), SoftDel_AndSqlIsNonDel_NoSoftDelCol_Empty!);
		R(nameof(SoftDel_SqlIsNonDel_NoSoftDelCol_Throws), SoftDel_SqlIsNonDel_NoSoftDelCol_Throws!);
	}

	/// <summary>配置 SoftDelCol 後,「未刪除」條件為「(軟刪列 = 0)」。</summary>
	public partial async Task<nil> SoftDel_SqlIsNonDel_WithSoftDelCol_Exact(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_SoftDel_IsNonDel");
		tbl.SoftDelCol = new SoftDelol {
			CodeColName = nameof(TestKv.DelAt),
			FnSqlIsDel = ()=>tbl.QtCol(nameof(TestKv.DelAt)) + "<>0",
			FnSqlIsNonDel = ()=>tbl.QtCol(nameof(TestKv.DelAt)) + "=0",
		};
		T(tbl.SqlIsNonDel() == "(\"DelAt\" = 0)", $"SQL 不匹配.\n{tbl.SqlIsNonDel()}");
		return NIL;
	}

	/// <summary>AndSqlIsNonDel 在 SqlIsNonDel 前加 AND,可直接拼進 WHERE 之後。</summary>
	public partial async Task<nil> SoftDel_AndSqlIsNonDel_WithSoftDelCol_Exact(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_SoftDel_AndIsNonDel");
		tbl.SoftDelCol = new SoftDelol {
			CodeColName = nameof(TestKv.DelAt),
			FnSqlIsDel = ()=>tbl.QtCol(nameof(TestKv.DelAt)) + "<>0",
			FnSqlIsNonDel = ()=>tbl.QtCol(nameof(TestKv.DelAt)) + "=0",
		};
		T(tbl.AndSqlIsNonDel() == "AND (\"DelAt\" = 0)", $"SQL 不匹配.\n{tbl.AndSqlIsNonDel()}");
		return NIL;
	}

	/// <summary>未配置 SoftDelCol 時,AndSqlIsNonDel 返回空串,不污染 WHERE。</summary>
	public partial async Task<nil> SoftDel_AndSqlIsNonDel_NoSoftDelCol_Empty(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_SoftDel_NoCol_And");
		T(tbl.AndSqlIsNonDel() == "", "應返回空串.");
		return NIL;
	}

	/// <summary>未配置 SoftDelCol 卻直接取「未刪除條件」屬配置錯誤,必須報錯。</summary>
	public partial async Task<nil> SoftDel_SqlIsNonDel_NoSoftDelCol_Throws(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_SoftDel_NoCol_IsNonDel");
		var thrown = false;
		try {
			tbl.SqlIsNonDel();
		} catch (InvalidOperationException ex) {
			thrown = true;
			T(ex.Message.Contains("Soft delete column is not defined"), "錯誤信息應指明未定義軟刪列.");
		}
		T(thrown, "應拋 InvalidOperationException.");
		return NIL;
	}
}
