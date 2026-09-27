using Tsinswreng.CsRefl;

namespace Tsinswreng.CsSql.Test.Domains;

/// <summary>
/// 測試域的型別元資料來源。本域實體型別交給反射來源，故不必逐個登記型別，
/// 也不需要源生成器；CsSql 的 Table 與 SqlRepo 都用它按名讀寫實體。
/// </summary>
public class TestDictMapper {
	protected static TestDictMapper? _Inst = null;
	public static TestDictMapper Inst => _Inst ??= new TestDictMapper();

	/// <summary>型別元資料來源：反射實現，任何型別都能查。</summary>
	public ITypeInfoSrc TypeInfoSrc { get; } = new ReflTypeInfoSrc();
}
