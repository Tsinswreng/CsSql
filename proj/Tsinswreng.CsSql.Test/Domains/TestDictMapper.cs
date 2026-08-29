using Tsinswreng.Srefl;

namespace Tsinswreng.CsSql.Test.Domains;

/// <summary>
/// 測試域屬性訪問器宿主。[SreflType] 源生成器為其生成
/// <see cref="IPropAccessorReg"/> 實現(CsSql 的 Table/SqlRepo 依賴它)。
/// </summary>
[SreflType(typeof(PoAllBasicTypes))]
[SreflType(typeof(TestKv))]
[SreflType(typeof(TestWord))]
[SreflType(typeof(TestWordProp))]
[SreflType(typeof(TestWordLearn))]
[SreflType(typeof(TestJnWord))]
public partial class TestDictMapper {
	protected static TestDictMapper? _Inst = null;
	public static TestDictMapper Inst => _Inst ??= new TestDictMapper();
}
