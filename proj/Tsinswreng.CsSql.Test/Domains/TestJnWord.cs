namespace Tsinswreng.CsSql.Test.Domains;

/// <summary>聚合體:單詞 + 屬性列表 + 學習記錄列表。對應 Ngan.Dict 的 JnWord 角色。</summary>
public class TestJnWord {
	public TestJnWord() {
	}

	public TestJnWord(
		TestWord Word
		,IList<TestWordProp> Props
		,IList<TestWordLearn> Learns
	) {
		this.Word = Word;
		this.Props = Props;
		this.Learns = Learns;
	}

	public TestWord Word { get; set; } = null!;

	public IList<TestWordProp> Props { get; set; } = [];

	public IList<TestWordLearn> Learns { get; set; } = [];
}
