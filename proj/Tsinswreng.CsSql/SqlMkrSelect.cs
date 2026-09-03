using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

namespace Tsinswreng.CsSql;

/// SELECT 構造器（T.SqlMkr().Select(...) 的鏈式返回類型）。
///
/// 語法順序示意：
/// Select(cols) → From() → [Where1() | WhereNonDel() | Where(raw)] → 條件鏈 → OrderBy → LimOfst → Build。
/// 條件鏈：AndEq（常量綁值）/ AndEqEach（同構批量等值：每元素一條語句，位置對齊）/
///         AndKeys（複合鍵批量：每行一條語句）/ AndIn（單語句 IN）/ And（任意運算符 Raw）...。
/// 所有值參數直接傳 Upper 值（構造即綁值），不需要 out IParam / binder。
public partial class SqlMkrSelect{
	/// 綁定的表。
	public ITable Tbl{get;set;}

	/// 已拼 SQL 段（含佔位符）緩衝。
	public IList<obj> Segs{get;set;} = [];

	/// 共享參數表（raw 值、名字全程唯一）。
	public IArgDict Args{get;set;} = ArgDict.Mk();

	/// 同構批量份數：AndEqEach/AndKeys 每調用一次加一份。
	public u64 TmplCnt{get;set;} = 1;

	// ===================== 表源 =====================

	/// FROM 主表（自 Tbl.DbTblName，自動加引號）。
	public partial SqlMkrSelect From();

	/// FROM 自定義表源（子查詢/多表 JOIN 等原始 SQL）。
	public partial SqlMkrSelect From(str RawTblSql);

	// ===================== WHERE 錨點 =====================

	/// WHERE 1=1 錨點：後續 And/Or 條件以 " AND ..." 拼接。
	public partial SqlMkrSelect Where1();

	/// WHERE 非刪錨點：表配置了 SoftDelCol 時自動追加 `softdel 非刪` 條件。
	/// 等價於 Where1().AndSqlIsNonDel()；無配置時等同 Where1()。
	public partial SqlMkrSelect WhereNonDel();

	/// WHERE 原始 SQL 片段（整段替換，不用鏈式條件時用）。
	public partial SqlMkrSelect Where(str RawSql);

	// ===================== 條件（常量綁值） =====================

	/// AND 原始 SQL 片段（逃逸通道：手寫子查詢/表達式）。
	public partial SqlMkrSelect And(str RawSql);

	/// AND 非刪條件：表配置了 SoftDelCol 時追加 `AND {softdel} 非刪`，無配置時為空操作。
	public partial SqlMkrSelect AndSqlIsNonDel();

	/// AND 等值：`CodeCol = @prm`，Upper 值自動轉 raw、參數名預設為 CodeCol。
	public partial SqlMkrSelect AndEq(str CodeCol, obj? UpperVal);

	/// AND 任意運算符：`CodeCol {Op} @prm`（Op 如 "=", ">", "LIKE"、值按需帶通配符）。
	public partial SqlMkrSelect And(str CodeCol, str Op, obj? UpperVal);

	/// OR 原始 SQL 片段（逃逸通道）。
	public partial SqlMkrSelect Or(str RawSql);

	/// OR 等值（常量綁值）。
	public partial SqlMkrSelect OrEq(str CodeCol, obj? UpperVal);

	// ===================== 條件（批量） =====================

	/// 同構批量等值：每元素一條 `... AND CodeCol = @CodeCol__i` 語句，
	/// 全部拼進同一命令，TmplCnt 加一份——執行層 AsyE1dWithNull 逐結果集收首行、位置對齊補 null。
	/// 這是「IN 做不了」的保序/查無補 null 查詢的入口（對標舊 RunDupliSql + Many binder）。
	/// 泛型 T 使 IList<IdWord> 等值類型 Id 不需要手動轉 obj?。
	public partial SqlMkrSelect AndEqEach<T>(str CodeCol, IList<T> Uppers);

	/// 複合鍵批量：每行一個鍵字典（CodeCol → Upper 值），
	/// 每行生成一條 `... AND (k1 = @k1__i AND k2 = @k2__i ...)` 語句，位置對齊回讀。
	/// 適合 (Owner, Head, Lang) 這類多列鍵批量查（舊 One+Many binder 的替代）。
	public partial SqlMkrSelect AndKeys(IList<IStr_Any> Keys);

	/// 單語句 IN：`CodeCol IN (@p0, @p1, ...)`，一條語句、查無缺行（無序）。
	/// 返回行數 = 命中數，不是入參數——需要「一一對應」語義請用 AndEqEach/AndKeys。
	public partial SqlMkrSelect AndIn<T>(str CodeCol, IList<T> Uppers);

	// ===================== 排序 / 分頁 =====================

	/// ORDER BY 多列（CodeColName，自動加引號）。
	public partial SqlMkrSelect OrderBy(params str[] CodeCols);

	/// ORDER BY ... DESC 多列。
	public partial SqlMkrSelect OrderByDesc(params str[] CodeCols);

	/// LIMIT n（方言化）。
	public partial SqlMkrSelect Lim(u64 Limit);

	/// OFFSET n（方言化）。
	public partial SqlMkrSelect Ofst(u64 Ofst);

	/// LIMIT/OFFSET 一次綁定（IPageQry→PageSize/Offset 自動入參）。
	public partial SqlMkrSelect LimOfst(IPageQry PageQry);

	// ===================== 產物 =====================

	/// 拼出最終 SqlStmt（Sql 文本 + Args + TmplCnt）。
	public partial SqlStmt Build();
}