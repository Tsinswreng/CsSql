namespace Tsinswreng.CsSql.Test.Domains;

/// <summary>聚合子:單詞學習記錄。對應 Ngan.Dict 的 PoWordLearn 角色。外鍵 WordId 指向聚合根。</summary>
public class TestWordLearn {
	public IdTestWordLearn Id { get; set; } = new();

	public IdTestWord WordId { get; set; } = default;

	public str LearnResult { get; set; } = "";

	/// <summary>軟刪標記列:0=未刪,非0=已刪。</summary>
	public i64 DelAt { get; set; } = 0;
}
