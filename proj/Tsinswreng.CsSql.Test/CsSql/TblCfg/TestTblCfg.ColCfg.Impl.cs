using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊 ColCfg 各用例。</summary>
	public partial void RegisterColCfg(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestTblCfg)
			,[typeof(ITable<TestKv>)]
			,[nameof(ExtnColMkr.Col)]
			,nameof(TestTblCfg)
		);
		var R = register.Register;

		R(nameof(ColCfg_TypeTwoGeneric_SetsRawUpperDbType), ColCfg_TypeTwoGeneric_SetsRawUpperDbType!);
		R(nameof(ColCfg_TypeOneGeneric_RawUpperSame), ColCfg_TypeOneGeneric_RawUpperSame!);
		R(nameof(ColCfg_NotNull_SetsFlag), ColCfg_NotNull_SetsFlag!);
		R(nameof(ColCfg_AdditionalSqls_AppendsInOrder), ColCfg_AdditionalSqls_AppendsInOrder!);
		R(nameof(ColCfg_DbName_RenamesDbColumn), ColCfg_DbName_RenamesDbColumn!);
		R(nameof(ColCfg_ColByNameAndExpr_SameColumn), ColCfg_ColByNameAndExpr_SameColumn!);
		R(nameof(ColCfg_GetCol_Missing_ThrowsWithAvailableCols), ColCfg_GetCol_Missing_ThrowsWithAvailableCols!);
		R(nameof(ColCfg_MapType_SetsRawClrType_AndRegistersDfltMapper), ColCfg_MapType_SetsRawClrType_AndRegistersDfltMapper!);
	}

	/// <summary>Type&lt;TRaw,TUpper&gt;(dbType):RawClrType/UpperClrType/DbType 三者一併設置。</summary>
	public partial async Task<nil> ColCfg_TypeTwoGeneric_SetsRawUpperDbType(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_TypeTwo");
		tbl.Col(nameof(TestKv.DelAt)).Type<i64, i64>("INTEGER");
		var col = tbl.GetCol(nameof(TestKv.DelAt));
		T(col.DbType == "INTEGER", "DbType 未生效.");
		T(col.RawClrType == typeof(i64), "RawClrType 未生效.");
		T(col.UpperClrType == typeof(i64), "UpperClrType 未生效.");
		return NIL;
	}

	/// <summary>單泛型 Type&lt;TRaw&gt;(dbType) 是雙泛型的簡寫:Raw 與 Upper 同類型。</summary>
	public partial async Task<nil> ColCfg_TypeOneGeneric_RawUpperSame(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_TypeOne");
		tbl.Col(nameof(TestKv.DelAt)).Type<i64>("INTEGER");
		var col = tbl.GetCol(nameof(TestKv.DelAt));
		T(col.RawClrType == typeof(i64) && col.UpperClrType == typeof(i64), "Raw 與 Upper 應同為 i64.");
		T(col.DbType == "INTEGER", "DbType 未生效.");
		return NIL;
	}

	/// <summary>NotNull() 翻轉列的 NotNull 標記,影響 SqlMkTbl 的 NOT NULL 輸出。</summary>
	public partial async Task<nil> ColCfg_NotNull_SetsFlag(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_NotNull");
		tbl.Col(nameof(TestKv.DelAt)).NotNull();
		T(tbl.GetCol(nameof(TestKv.DelAt)).NotNull, "NotNull 標記未生效.");
		return NIL;
	}

	/// <summary>AdditionalSqls 多次調用是追加而非覆蓋,順序保持,直接影響 DDL(如 PRIMARY KEY 位置)。</summary>
	public partial async Task<nil> ColCfg_AdditionalSqls_AppendsInOrder(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_AddSqls");
		tbl.Col(nameof(TestKv.DelAt)).AdditionalSqls(["UNIQUE"]).AdditionalSqls(["DEFAULT 0"]);
		var sqls = tbl.GetCol(nameof(TestKv.DelAt)).AdditionalSqls;
		T(sqls.Count == 2, "應追加兩條.");
		T(sqls[0] == "UNIQUE" && sqls[1] == "DEFAULT 0", "追加順序應保持.");
		return NIL;
	}

	/// <summary>DbName 改名後,DbColName/QtCol 都按新名輸出(實體屬性名不變,只換庫列名)。</summary>
	public partial async Task<nil> ColCfg_DbName_RenamesDbColumn(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_DbName");
		tbl.Col(nameof(TestKv.DelAt)).DbName("deleted_at");
		T(tbl.DbColName(nameof(TestKv.DelAt)) == "deleted_at", "DbColName 應返回新名.");
		T(tbl.QtCol(nameof(TestKv.DelAt)) == "\"deleted_at\"", "QtCol 應引用新名.");
		return NIL;
	}

	/// <summary>Col(屬性名字符串) 與 Col(成員表達式) 應指向同一個 IColumn 對象。</summary>
	public partial async Task<nil> ColCfg_ColByNameAndExpr_SameColumn(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_SameCol");
		var byName = tbl.Col(nameof(TestKv.DelAt));
		var byExpr = tbl.Col(x=>x.DelAt);
		T(ReferenceEquals(byName.Column, byExpr.Column), "兩種取列方式應得到同一列.");
		return NIL;
	}

	/// <summary>GetCol 對不存在的列應報錯,且錯誤信息列出可用列,方便排查拼寫錯誤。</summary>
	public partial async Task<nil> ColCfg_GetCol_Missing_ThrowsWithAvailableCols(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_GetCol");
		var thrown = false;
		try {
			tbl.GetCol("NoSuchCol");
		} catch (Exception ex) {
			thrown = true;
			T(ex.Message.Contains("NoSuchCol"), "錯誤信息應含所查列名.");
			T(ex.Message.Contains(nameof(TestKv.Id)), "錯誤信息應列出可用列.");
		}
		T(thrown, "應拋異常.");
		return NIL;
	}

	/// <summary>MapType 把 RawClrType 設為 u8[](決定 SqlMkTbl 走 BLOB/bytea),同時把映射 TryAdd 進 UpperType_DfltMapper:同類型其他列自動沿用此默認映射。</summary>
	public partial async Task<nil> ColCfg_MapType_SetsRawClrType_AndRegistersDfltMapper(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkTbl("TblCfg_ColCfg_MapType");
		tbl.Col(nameof(TestKv.Owner)).MapType(IdTestUser.MkTypeMapFn());
		var col = tbl.GetCol(nameof(TestKv.Owner));
		T(col.RawClrType == typeof(u8[]), "RawClrType 應為 u8[].");
		T(col.UpperClrType == typeof(IdTestUser), "UpperClrType 應為 IdTestUser.");
		T(tbl.UpperType_DfltMapper.ContainsKey(typeof(IdTestUser)), "應註冊進 UpperType_DfltMapper.");
		return NIL;
	}
}
