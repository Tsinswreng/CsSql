using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

/// <summary>
/// 配置層(SchemaCfg)測試:Table/IColumn/ColMkr/ExtnITable/ITblMgr 的 SQL 生成行為。
/// 全部為純字符串斷言,不開 DB 連接、不建表;sqlite/pg 兩個入口共用同一批用例。
/// 斷言策略:列以顯式 DbType 為主使兩端輸出一致,僅「無 DbType 走類型映射」按 DbSrcType 分支預期。
/// </summary>
public partial class TestTblCfg : ITester {
	/// <summary>DI 提供的表註冊中心。測試只借用它的 SqlMkr(Quote/Param 行為),不註冊任何表、不碰 DB。</summary>
	readonly ITblMgr TblMgr;

	/// <summary>建配置層測試器,依賴由測試管理員的 DI 容器提供。</summary>
	public TestTblCfg(
		ITblMgr TblMgr
	) {
		this.TblMgr = TblMgr;
	}

	/// <summary>組裝配置層各 API 的測試節點。</summary>
	public ITestNode RegisterTestsInto(ITestNode? Test) {
		Test ??= new TestNode();
		Test.Ordered = true;

		RegisterSqlMkTbl(Test);
		RegisterColCfg(Test);
		RegisterClauseGen(Test);
		RegisterSoftDel(Test);
		return Test;
	}

	/// <summary>建一張 DbTblName 為 TblName 的 TestKv 表,掛上 DI 的 TblMgr 以獲得 SqlMkr。列由 Init 按實體屬性掃出。</summary>
	ITable<TestKv> MkTbl(str TblName) {
		var tbl = Table.Mk<TestKv>(TestDictMapper.Inst, TblName);
		tbl.TblMgr = TblMgr;
		return tbl;
	}

	/// <summary>
	/// 建一張手動裝列的表,完全控制列順序與列定義。
	/// SqlMkTbl 只依賴 Columns 與 SqlMkr,不需要 PropAccessorReg 的實體列掃描;
	/// 手動裝列使精確字符串斷言不依賴 accessor 的列枚舉順序。
	/// </summary>
	ITable MkRawTbl(
		str TblName
		,params (str Code, str DbName, Type? RawClrType, str DbType, bool NotNull, str[] AddSqls)[] Cols
	) {
		var tbl = new Table {
			PropAccessorReg = TestDictMapper.Inst,
			DbTblName = TblName,
			TblMgr = TblMgr,
		};
		foreach (var c in Cols) {
			var col = new Column {
				DbName = c.DbName,
				RawClrType = c.RawClrType,
				DbType = c.DbType,
				NotNull = c.NotNull,
			};
			foreach (var s in c.AddSqls) {
				col.AdditionalSqls.Add(s);
			}
			tbl.Columns[c.Code] = col;
		}
		return tbl;
	}

	/// <summary>統一換行,消除 Windows \r\n 與其他平台 \n 的差異。</summary>
	static str NormLf(str s) {
		return s.Replace("\r\n", "\n");
	}

	/// <summary>把字符串轉成可讀轉義形式,用於定位隱藏的換行/縮進差異。</summary>
	static str Esc(str s) {
		return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
	}

	/// <summary>斷言兩段 SQL 精確一致(忽略換行符差異);失敗時輸出轉義形式輔助定位。</summary>
	static void AssertSqlExact(str Actual, str Expected, str CaseName) {
		var T = Assert.IsTrue;
		var a = NormLf(Actual);
		var e = NormLf(Expected);
		T(a == e, $"{CaseName}: SQL mismatch.\nExpectedEsc:\n{Esc(e)}\nActualEsc:\n{Esc(a)}");
	}
}
