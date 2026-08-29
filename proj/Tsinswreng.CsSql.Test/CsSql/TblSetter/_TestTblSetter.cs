using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblSetter;

public partial class TestTblSetter : ITester {
	readonly ITblMgr TblMgr;

	public TestTblSetter(
		ITblMgr TblMgr
	) {
		this.TblMgr = TblMgr;
	}

	public ITestNode RegisterTestsInto(ITestNode? Test) {
		Test ??= new TestNode();
		Test.Ordered = true;

		RegisterIdx(Test);
		RegisterIdxExpr(Test);
		return Test;
	}

	ITblSetter<TestKv> MkTblSetter() {
		return new TblSetter<TestKv>(TblMgr.GetTbl<TestKv>());
	}

	static str NormLf(str s) {
		return s.Replace("\r\n", "\n");
	}

	static void AssertSqlListExact(
		IList<str> Actual
		,IList<str> Expected
		,str CaseName
	) {
		var T = Assert.IsTrue;
		T(Actual.Count == Expected.Count, $"{CaseName}: expected {Expected.Count} SQL rows, got {Actual.Count}");
		for (var i = 0; i < Expected.Count; i++) {
			var a = NormLf(Actual[i]);
			var e = NormLf(Expected[i]);
			T(a == e, $"{CaseName}: SQL[{i}] mismatch.\nExpected:\n{e}\nActual:\n{a}");
		}
	}

	static void AssertFnSetIdxPointsToDefault(
		ITblSetter<TestKv> Setter
		,str CaseName
	) {
		var T = Assert.IsTrue;
		T(Setter is TblSetter<TestKv>, $"{CaseName}: expected concrete TblSetter<TestKv>.");
		var impl = (TblSetter<TestKv>)Setter;
		var d = Setter.FnSetIdx;
		T(d.Method.Name == nameof(TblSetter<TestKv>.DefaultFnSetIdx), $"{CaseName}: FnSetIdx should point to DefaultFnSetIdx, got {d.Method.Name}.");
		T(object.ReferenceEquals(d.Target, impl), $"{CaseName}: FnSetIdx target should be current TblSetter instance.");
	}
}
