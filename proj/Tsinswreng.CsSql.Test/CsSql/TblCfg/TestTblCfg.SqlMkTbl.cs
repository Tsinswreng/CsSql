using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊 SqlMkTbl(CREATE TABLE DDL 生成)的用例。</summary>
	public partial void RegisterSqlMkTbl(ITestNode Node);

	/// <summary>顯式 DbType 列 + 主鍵 + NOT NULL 的精確 DDL 斷言。</summary>
	public partial Task<nil> SqlMkTbl_ExplicitDbType_Columns_PkNotNull_ExactDdl(obj? O);

	/// <summary>無 DbType 時按 RawClrType 走各 DB 類型映射器(sqlite/pg 分支預期)。</summary>
	public partial Task<nil> SqlMkTbl_NoDbType_MapperFallback_DbSpecificType(obj? O);

	/// <summary>InnerAdditionalSqls 以「,\n\n」內聯在右括號前。</summary>
	public partial Task<nil> SqlMkTbl_InnerAdditionalSqls_InlinedBeforeCloseParen(obj? O);

	/// <summary>OuterAdditionalSqls(如索引)以「sql;\n」追加在 CREATE TABLE 之後。</summary>
	public partial Task<nil> SqlMkTbl_OuterAdditionalSqls_AppendedWithSemicolon(obj? O);

	/// <summary>列既無 DbType 也無 RawClrType 時必須報錯。</summary>
	public partial Task<nil> SqlMkTbl_RawClrTypeNull_Throws(obj? O);

	/// <summary>類型映射器不認識的 RawClrType 報錯,且錯誤信息含「表.列」。</summary>
	public partial Task<nil> SqlMkTbl_UnmappableType_ThrowsWithTableColumn(obj? O);
}
