using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊軟刪 SQL 生成(SqlIsNonDel/AndSqlIsNonDel)的用例,覆蓋有/無 SoftDelCol 兩分支。</summary>
	public partial void RegisterSoftDel(ITestNode Node);

	/// <summary>配置 SoftDelCol 後,「未刪除」條件為「(軟刪列 = 0)」。</summary>
	public partial Task<nil> SoftDel_SqlIsNonDel_WithSoftDelCol_Exact(obj? O);

	/// <summary>AndSqlIsNonDel 在 SqlIsNonDel 前加 AND,可直接拼進 WHERE 之後。</summary>
	public partial Task<nil> SoftDel_AndSqlIsNonDel_WithSoftDelCol_Exact(obj? O);

	/// <summary>未配置 SoftDelCol 時,AndSqlIsNonDel 返回空串,不污染 WHERE。</summary>
	public partial Task<nil> SoftDel_AndSqlIsNonDel_NoSoftDelCol_Empty(obj? O);

	/// <summary>未配置 SoftDelCol 卻直接取「未刪除條件」屬配置錯誤,必須報錯。</summary>
	public partial Task<nil> SoftDel_SqlIsNonDel_NoSoftDelCol_Throws(obj? O);
}
