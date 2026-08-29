using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

/// 為 IRepo.OrdSoftDelById 提供測試(合併了 Ngan.Dict 的聲明/實現兩個文件)。</summary>
public partial class TestRepo {
	/// <summary>驗證有序軟刪會標記已存在資料、忽略不存在 ID,且空輸入不會破壞資料。</summary>
	void RegisterOrdSoftDelById(ITestNode Node) {
		var Register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[nameof(IRepo<TestKv, IdTestKv>.OrdSoftDelById)]
		);
		var T = Assert.IsTrue;
		Register.Register(
			"OrdSoftDelByIdMarksExistingRowsAndAcceptsMissingOrEmptyIds"
			,async(o)=>{
				return await RunInTxnIfNoCtx(async(Ctx)=>{
					var First = MkOrdSoftDelEntity("first");
					var Second = MkOrdSoftDelEntity("second");
					var MissingId = new IdTestKv();

					try {
						// 先插入兩筆唯一資料,再把不存在的 ID 混入批次,驗證批次位置不會造成例外。
						await Repo.OrdAdd(Ctx, AsyE(First, Second), CT.None);
						var Resp = await Repo.OrdSoftDelById(
							Ctx
							,AsyE(First.Id, MissingId, Second.Id)
							,CT.None
						);
						T(Resp is not null);

						// 空批次應是安全的 no-op,且不能改變前一步已建立的刪除狀態。
						var EmptyResp = await Repo.OrdSoftDelById(Ctx, AsyE<IdTestKv>(), CT.None);
						T(EmptyResp is not null);

						// 普通讀取必須排除軟刪資料;WithDel 讀取則須保留原順序及刪除標記。
						var AliveRows = await Repo.OrdGetById(
							Ctx
							,AsyE(First.Id, Second.Id)
							,CT.None
						).ToListAsync(CT.None);
						T(AliveRows.Count == 2);
						T(AliveRows.All(X=>X is null));

						var DeletedRows = await Repo.OrdGetByIdWithDel(
							Ctx
							,AsyE(First.Id, Second.Id)
							,CT.None
						).ToListAsync(CT.None);
						T(DeletedRows.Count == 2);
						T(DeletedRows.All(X=>X is not null && X.DelAt != 0));
						return NIL;
					} finally {
						// 無論任何斷言是否失敗,都硬刪本用例建立的資料,避免污染共享測試庫。
						await Repo.OrdHardDelById(Ctx, AsyE(First.Id, Second.Id), CT.None);
					}
				});
			}
		);
	}

	/// <summary>建立鍵和值均帶 GUID 的測試資料,避免與既有或並行測試資料重複。</summary>
	static TestKv MkOrdSoftDelEntity(str Label) {
		var Suffix = Guid.NewGuid().ToString("N");
		return new TestKv{
			Id = new IdTestKv(),
			Owner = IdTestUser.Zero,
			KStr = $"ord_soft_del_{Label}_{Suffix}",
			VStr = $"ord_soft_del_value_{Label}_{Suffix}",
		};
	}
}
