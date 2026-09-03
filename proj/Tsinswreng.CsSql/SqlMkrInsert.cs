using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

namespace Tsinswreng.CsSql;

/// INSERT 構造器（T.SqlMkr().Insert(CodeCols) 的鏈式返回類型）。
///
/// 同構多行 INSERT：構造時聲明列集，AddRow/AddRows 逐行/整批喂值，
/// Build 產出單條 `INSERT INTO t (cols) VALUES (...),(...),...`（pg 高效通道；sqlite 批 1 由上游切批）。
/// 每行是 CodeCol → Upper 值的字典，缺構造列在 Build 時報錯。
public partial class SqlMkrInsert{
	/// 綁定的表。
	public ITable Tbl{get;set;}

	/// 構造時聲明的列集（CodeColName，同構必填）。
	public IList<str> CodeCols{get;set;} = [];

	/// 累積的行（每行 CodeCol → Upper 值）。
	public IList<IStr_Any> Rows{get;set;} = [];

	/// 追加一行（整個批可以逐行拼，也可以一次 AddRows）。
	public partial SqlMkrInsert AddRow(IStr_Any CodeCol_UpperVal);

	/// 整批追加：函數只管一批的直譯——把 IList 行原樣收下。
	public partial SqlMkrInsert AddRows(IList<IStr_Any> Rows);

	/// 拼出 SqlStmt（單語句多組 VALUES、參數自動帶 `__組號` 後綴、值 Upper→Raw）。
	public partial SqlStmt Build();
}