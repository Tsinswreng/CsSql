namespace Tsinswreng.CsSql;

/// SqlMkr：構造型「一批一次執行」的 SQL 構造器（取代舊 SqlSplicer 的職責）。
///
/// 設計總則（新批量寫法）：
/// - 函數邊界 = 批邊界：整個 SqlMkr 生命週期收集的數據就是「一批」，
///   一口氣 Build 成一個命令可執行的 SQL 文本 + 參數表（詳見 SqlStmt）；
///   分批/切批不在此處，由最源頭的調用方（SqlFlow）負責。
/// - 構造即綁值：條件和值在鏈式調用時直接傳入（obj? Upper 值），
///   不再有 SqlSplicer 的 out IParam / binder / Many 流式綁定心智。
/// - 列名入口統一用 CodeColName：內部自動映射 Db 列名、加引號、Upper→Raw 轉換。
/// - 分型入口：Select / Insert / Update / Delete 各自返回專用構造器，
///   語法順序由類型保證（例如 INSERT 構造器上沒有 OrderBy）。
///
/// 入口：`T.SqlMkr()`（見 ExtnITable.SqlMkr）。
public partial class SqlMkr{
	/// 綁定的表：列引用、值轉換、方言（引號/參數前綴）都從它來。
	public ITable Tbl{get;set;}

	// ===================== 語句入口 =====================

	/// 進入 SELECT 構造器。CodeCols 為要選取的 CodeColName 列表；
	/// 傳 "*" 表示全列（對齊 SqlSplicer.Select("*") 用法）。
	public partial SqlMkrSelect Select(params str[] CodeCols);

	/// 進入 INSERT 構造器。CodeCols 聲明這一批共有的列集（同構 INSERT），
	/// 之後 AddRow/AddRows 的每行字典的鍵必須與其一致（缺列在 Build 時報錯）。
	public partial SqlMkrInsert Insert(IEnumerable<str> CodeCols);

	/// 進入 UPDATE 構造器。異構語義：每一對 (Id, 列集) 的 SET 列集自行決定，
	/// 由後續 AddRow/AddRows 逐對提供（同構只是各對鍵集恰好相同）；
	/// dict 鍵 = SET 列集，Id 鍵 = WHERE，因此改主鍵（SET 含主鍵列）同樣可表達。
	public partial SqlMkrUpdate Update();

	/// 進入 DELETE 構造器。支持按 IN 條件的硬刪（AndIn/HardIn）、
	/// 軟刪（SoftIn，需表配置 SoftDelCol）以及 Raw 手拼逃逸（多表子查詢等）。
	public partial SqlMkrDelete Delete();
}