namespace Tsinswreng.CsSql.Test.Domains;

/// <summary>
/// 測試域「鍵值對」實體,對應 Ngan.Dict 的 PoKv 角色。
/// 覆蓋:強類型 Id 主鍵、普通字符串字段、軟刪列(DelAt)。
/// 軟刪語義:DelAt=0 未刪;非 0 已刪。
/// </summary>
public class TestKv {
	public IdTestKv Id { get; set; } = new();

	public IdTestUser Owner { get; set; } = default;

	public str KStr { get; set; } = "";

	public str VStr { get; set; } = "";

	/// <summary>軟刪標記列:0=未刪,非0=已刪。</summary>
	public i64 DelAt { get; set; } = 0;
}
