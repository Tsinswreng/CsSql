using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

/// <summary>
/// IRepo 功能測試。用例遷移自 Ngan.Dict.Backend.Test 的 TestRepo,
/// 實體換成 CsSql 測試域自帶的(強類型 Id + 軟刪)。
/// </summary>
public partial class TestRepo : ITester {
	/// <summary>SQL 命令建立器,讓每個資料庫用例在同一交易上下文中執行。</summary>
	readonly ISqlCmdMkr SqlCmdMkr;

	/// <summary>資料表註冊中心,供需要直接測試 ITable overload 的用例取得表定義。</summary>
	readonly ITblMgr TblMgr;

	/// <summary>測試域通用鍵值資料的 Repository。</summary>
	readonly IRepo<TestKv, IdTestKv> Repo;

	/// <summary>聚合根 Repository。</summary>
	readonly IRepo<TestWord, IdTestWord> RepoWord;

	/// <summary>聚合子(屬性) Repository。</summary>
	readonly IRepo<TestWordProp, IdTestWordProp> RepoProp;

	/// <summary>聚合子(學習記錄) Repository。</summary>
	readonly IRepo<TestWordLearn, IdTestWordLearn> RepoLearn;

	/// <summary>建立 Repository 測試器,依賴均由測試管理員的 DI 容器提供。</summary>
	public TestRepo(
		ISqlCmdMkr SqlCmdMkr
		,ITblMgr TblMgr
		,IRepo<TestKv, IdTestKv> Repo
		,IRepo<TestWord, IdTestWord> RepoWord
		,IRepo<TestWordProp, IdTestWordProp> RepoProp
		,IRepo<TestWordLearn, IdTestWordLearn> RepoLearn
	) {
		this.SqlCmdMkr = SqlCmdMkr;
		this.TblMgr = TblMgr;
		this.Repo = Repo;
		this.RepoWord = RepoWord;
		this.RepoProp = RepoProp;
		this.RepoLearn = RepoLearn;
	}

	/// <summary>組裝 IRepo 各 API 的測試節點;資料庫用例保持順序執行,避免共享庫互相干擾。</summary>
	public ITestNode RegisterTestsInto(ITestNode? Test) {
		Test ??= new TestNode();
		Test.Ordered = true;

		RegisterSlctManyInIdsWithDel(Test);
		RegisterBatSlctById(Test);
		RegisterBatInsert(Test);
		RegisterBatUpd(Test);
		RegisterBatExistsAndUpsert(Test);
		RegisterDelInId(Test);
		RegisterOrdSoftDelById(Test);
		RegisterGetAll(Test);
		RegisterAgg(Test);
		RegisterIncludeEntitysByKeys(Test);
		return Test;
	}

	/// <summary>將少量固定測試資料轉成異步序列,對齊 IRepo 的批次 API。</summary>
	private static async IAsyncEnumerable<T> AsyE<T>(params T[] Items) {
		foreach (var Item in Items) {
			yield return Item;
		}
	}

	/// <summary>在獨立交易上下文中執行一個測試步驟,並由框架統一釋放資料庫資源。</summary>
	private Task<TRtn> RunInTxnIfNoCtx<TRtn>(Func<IDbFnCtx, Task<TRtn>> Fn) {
		return SqlCmdMkr.EnsureTxn(null, CT.None, Fn);
	}
}
