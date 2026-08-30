using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊 SqlMkTbl 各用例。</summary>
	public partial void RegisterSqlMkTbl(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestTblCfg)
			,[typeof(ITable)]
			,[nameof(ExtnITable.SqlMkTbl)]
			,nameof(TestTblCfg)
		);
		var R = register.Register;

		R(nameof(SqlMkTbl_ExplicitDbType_Columns_PkNotNull_ExactDdl), SqlMkTbl_ExplicitDbType_Columns_PkNotNull_ExactDdl!);
		R(nameof(SqlMkTbl_NoDbType_MapperFallback_DbSpecificType), SqlMkTbl_NoDbType_MapperFallback_DbSpecificType!);
		R(nameof(SqlMkTbl_InnerAdditionalSqls_InlinedBeforeCloseParen), SqlMkTbl_InnerAdditionalSqls_InlinedBeforeCloseParen!);
		R(nameof(SqlMkTbl_OuterAdditionalSqls_AppendedWithSemicolon), SqlMkTbl_OuterAdditionalSqls_AppendedWithSemicolon!);
		R(nameof(SqlMkTbl_RawClrTypeNull_Throws), SqlMkTbl_RawClrTypeNull_Throws!);
		R(nameof(SqlMkTbl_UnmappableType_ThrowsWithTableColumn), SqlMkTbl_UnmappableType_ThrowsWithTableColumn!);
	}

	/// <summary>顯式 DbType 列 + 主鍵 + NOT NULL 的精確 DDL 斷言:列順序由手動裝列決定,不依賴 accessor 枚舉順序。</summary>
	public partial async Task<nil> SqlMkTbl_ExplicitDbType_Columns_PkNotNull_ExactDdl(obj? O) {
		var tbl = MkRawTbl(
			"TblCfg_SqlMkTbl_Explicit"
			,("Id", "Id", typeof(byte[]), "BLOB", true, ["PRIMARY KEY"])
			,("KStr", "KStr", typeof(str), "TEXT", true, [])
			,("VStr", "VStr", typeof(str), "TEXT", false, [])
		);
		var expected =
			"CREATE TABLE IF NOT EXISTS \"TblCfg_SqlMkTbl_Explicit\"(\n"
			+ "\t\"Id\" BLOB PRIMARY KEY NOT NULL,\n"
			+ "\t\"KStr\" TEXT NOT NULL,\n"
			+ "\t\"VStr\" TEXT\n"
			+ ");\n";
		AssertSqlExact(tbl.SqlMkTbl(), expected, nameof(SqlMkTbl_ExplicitDbType_Columns_PkNotNull_ExactDdl));
		return NIL;
	}

	/// <summary>無 DbType 時按 RawClrType 走各 DB 的類型映射器:sqlite 與 pg 類型名不同,按 DbSrcType 分支預期。</summary>
	public partial async Task<nil> SqlMkTbl_NoDbType_MapperFallback_DbSpecificType(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkRawTbl(
			"TblCfg_SqlMkTbl_Mapper"
			,("Id", "Id", typeof(byte[]), "", false, [])
			,("StrVal", "StrVal", typeof(str), "", false, [])
			,("U8Val", "U8Val", typeof(byte), "", false, [])
		);
		var isSqlite = TblMgr.DbSrcType == EDbSrcType.Sqlite;
		var blobType = isSqlite ? "BLOB" : "bytea";
		var textType = isSqlite ? "TEXT" : "text";
		var intType = isSqlite ? "INTEGER" : "smallint";

		var sql = NormLf(tbl.SqlMkTbl());
		T(sql.Contains($"\"Id\" {blobType}"), $"Id 列應映射為 {blobType}.\n{sql}");
		T(sql.Contains($"\"StrVal\" {textType}"), $"StrVal 列應映射為 {textType}.\n{sql}");
		T(sql.Contains($"\"U8Val\" {intType}"), $"U8Val 列應映射為 {intType}.\n{sql}");
		return NIL;
	}

	/// <summary>InnerAdditionalSqls 以「,\n\n」接在最後一列後、右括號前,保持現有輸出格式。</summary>
	public partial async Task<nil> SqlMkTbl_InnerAdditionalSqls_InlinedBeforeCloseParen(obj? O) {
		var tbl = MkRawTbl(
			"TblCfg_SqlMkTbl_Inner"
			,("Id", "Id", typeof(str), "TEXT", false, [])
		);
		tbl.InnerAdditionalSqls.Add("DEFAULT 'x'");
		var expected =
			"CREATE TABLE IF NOT EXISTS \"TblCfg_SqlMkTbl_Inner\"(\n"
			+ "\t\"Id\" TEXT,\n"
			+ "\n"
			+ "DEFAULT 'x'\n"
			+ ");\n";
		AssertSqlExact(tbl.SqlMkTbl(), expected, nameof(SqlMkTbl_InnerAdditionalSqls_InlinedBeforeCloseParen));
		return NIL;
	}

	/// <summary>OuterAdditionalSqls(如索引)以「sql;\n」追加在 CREATE TABLE 之後。</summary>
	public partial async Task<nil> SqlMkTbl_OuterAdditionalSqls_AppendedWithSemicolon(obj? O) {
		var tbl = MkRawTbl(
			"TblCfg_SqlMkTbl_Outer"
			,("Id", "Id", typeof(str), "TEXT", false, [])
		);
		tbl.OuterAdditionalSqls.Add("CREATE INDEX \"Ix\" ON \"TblCfg_SqlMkTbl_Outer\" (\"Id\")");
		var expected =
			"CREATE TABLE IF NOT EXISTS \"TblCfg_SqlMkTbl_Outer\"(\n"
			+ "\t\"Id\" TEXT\n"
			+ ");\n"
			+ "CREATE INDEX \"Ix\" ON \"TblCfg_SqlMkTbl_Outer\" (\"Id\");\n";
		AssertSqlExact(tbl.SqlMkTbl(), expected, nameof(SqlMkTbl_OuterAdditionalSqls_AppendedWithSemicolon));
		return NIL;
	}

	/// <summary>列既無 DbType 也無 RawClrType 時,無法決定數據庫類型,必須報錯。</summary>
	public partial async Task<nil> SqlMkTbl_RawClrTypeNull_Throws(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkRawTbl(
			"TblCfg_SqlMkTbl_RawNull"
			,("X", "X", null, "", false, [])
		);
		var thrown = false;
		try {
			tbl.SqlMkTbl();
		} catch (Exception ex) {
			thrown = true;
			T(ex.Message.Contains("Col.RawClrType == null"), $"錯誤信息應指明 RawClrType 為 null.\n{ex.Message}");
		}
		T(thrown, "應拋異常.");
		return NIL;
	}

	/// <summary>類型映射器不認識的 RawClrType(如強類型 Id struct)應報錯,且錯誤信息含表名與列名,方便定位配置。</summary>
	public partial async Task<nil> SqlMkTbl_UnmappableType_ThrowsWithTableColumn(obj? O) {
		var T = Assert.IsTrue;
		var tbl = MkRawTbl(
			"TblCfg_SqlMkTbl_Unmappable"
			,("X", "X", typeof(IdTestKv), "", false, [])
		);
		var thrown = false;
		try {
			tbl.SqlMkTbl();
		} catch (Exception ex) {
			thrown = true;
			T(ex.Message.Contains("TblCfg_SqlMkTbl_Unmappable.X"), $"錯誤信息應含「表.列」.\n{ex.Message}");
		}
		T(thrown, "應拋異常.");
		return NIL;
	}
}
