namespace Tsinswreng.CsSql.Test.Domains;

/// <summary>聚合根:單詞。對應 Ngan.Dict 的 PoWord 角色。</summary>
public class TestWord {
	public IdTestWord Id { get; set; } = new();

	public IdTestUser Owner { get; set; } = default;

	public str Head { get; set; } = "";

	public str Lang { get; set; } = "";

	/// <summary>軟刪標記列:0=未刪,非0=已刪。</summary>
	public i64 DelAt { get; set; } = 0;
}
