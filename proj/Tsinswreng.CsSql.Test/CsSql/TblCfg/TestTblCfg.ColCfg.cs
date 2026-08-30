using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblCfg;

public partial class TestTblCfg {
	/// <summary>註冊 ColMkr 配置 API(Type/NotNull/AdditionalSqls/DbName/MapType)對列元數據影響的用例。</summary>
	public partial void RegisterColCfg(ITestNode Node);

	/// <summary>Type&lt;TRaw,TUpper&gt;(dbType) 一併設置 RawClrType/UpperClrType/DbType。</summary>
	public partial Task<nil> ColCfg_TypeTwoGeneric_SetsRawUpperDbType(obj? O);

	/// <summary>單泛型 Type&lt;TRaw&gt;(dbType) 是雙泛型的簡寫:Raw 與 Upper 同類型。</summary>
	public partial Task<nil> ColCfg_TypeOneGeneric_RawUpperSame(obj? O);

	/// <summary>NotNull() 翻轉列的 NotNull 標記。</summary>
	public partial Task<nil> ColCfg_NotNull_SetsFlag(obj? O);

	/// <summary>AdditionalSqls 多次調用是追加而非覆蓋,順序保持。</summary>
	public partial Task<nil> ColCfg_AdditionalSqls_AppendsInOrder(obj? O);

	/// <summary>DbName 改名後,DbColName/QtCol 都按新名輸出。</summary>
	public partial Task<nil> ColCfg_DbName_RenamesDbColumn(obj? O);

	/// <summary>Col(屬性名字符串) 與 Col(成員表達式) 應指向同一個 IColumn 對象。</summary>
	public partial Task<nil> ColCfg_ColByNameAndExpr_SameColumn(obj? O);

	/// <summary>GetCol 對不存在的列報錯,且錯誤信息列出可用列。</summary>
	public partial Task<nil> ColCfg_GetCol_Missing_ThrowsWithAvailableCols(obj? O);

	/// <summary>MapType 設 RawClrType 為 u8[] 並把映射註冊進 UpperType_DfltMapper(類型級默認)。</summary>
	public partial Task<nil> ColCfg_MapType_SetsRawClrType_AndRegistersDfltMapper(obj? O);
}
