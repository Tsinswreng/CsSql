using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

namespace Tsinswreng.CsSql;

/// UPDATE 構造器（T.SqlMkr().Update() 的鏈式返回類型）。
///
/// 異構語義：每一對 = (Id 值, 該行要更新的列集 CodeCol → Upper 值)，
/// SET 段 = 傳入 dict 的鍵集（同行構只是各對鍵集恰好相同）。
/// Build 產出 `; ` 拼接的 N 條 `UPDATE t SET ... WHERE IdCol = @id__i`。
///
/// 兩種典型用法：
/// - 普通更新（對標 OrdUpdByCodeDict/OrdUpdByDbDict）：dict 不含主鍵列，WHERE 主鍵 = 傳入 Id 值；
/// - 改主鍵（對標 BatChangeWordId 手拼）：dict 含主鍵列（新值），WHERE 主鍵 = 傳入 Id 值（舊值）。
public partial class SqlMkrUpdate{
	/// 綁定的表。
	public ITable Tbl{get;set;}

	/// 累積的 (Id值, 該行要更新的列集)。
	public IList<(obj? IdUpper, IStr_Any CodeCol_UpperVals)> Pairs{get;set;} = [];

	/// WHERE 主鍵列的 CodeColName（由 AddRow/AddRows 首次調用時鎖定，全程唯一）。
	public str? IdCodeCol{get;set;}

	/// 追加一對：IdCodeCol 是 WHERE 主鍵列的 CodeColName；Id 是主鍵值；
	/// CodeCol_UpperVals 是該行 SET 列集（CodeCol → Upper 值）。SET 列集含 IdCodeCol 即「改主鍵」。
	public partial SqlMkrUpdate AddRow(str IdCodeCol, obj? IdUpper, IStr_Any CodeCol_UpperVals);

	/// 成對批量追加：Ids 與 CodeCol_UpperVals 按位對齊（長度不等在 Build 時報錯），
	/// 函數只管一批的直譯——把整批 Ids + 整批列集原樣收下。
	/// 泛型 T 使 IList<IdWord> 等值類型 Id 不需要手動轉 obj?。
	public partial SqlMkrUpdate AddRows<T>(str IdCodeCol, IList<T> Ids, IList<IStr_Any> CodeCol_UpperVals);

	/// 拼出 ISqlEtArg（N 條 UPDATE ';' 拼接、參數名自動帶對序、值 Upper→Raw）。
	public partial ISqlEtArg Build();
}