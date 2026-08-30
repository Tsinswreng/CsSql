using Tsinswreng.CsSql.Test.CsSql.Repo;
using Tsinswreng.CsSql.Test.CsSql.TblCfg;
using Tsinswreng.CsSql.Test.CsSql.TblSetter;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test;

/// <summary>
/// CsSql 測試管理員。註冊所有 tester,供 exe 入口(Test.Sqlite/Test.Postgres)收編。
/// </summary>
public class CsSqlTestMgr : DiEtTestMgr {
	public static CsSqlTestMgr Inst = new();
	public override ITestNode RegisterTestsInto(ITestNode? Test) {
		Test = this.TestNode;
		Test.Ordered = true;
		Test.IsParallelRecursive = true;  // 遞迴禁用並行,所有資料庫測試共用同一 sqlite 連接
		this.RegisterTester<TestRepo>();
		this.RegisterTester<TestTblSetter>();
		this.RegisterTester<TestTblCfg>();
		return Test;
	}
}
