using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<IdTestKv> _slctManyIds = new();
	IdTestKv? _slctManySoftDeletedId = null;

	void RegisterSlctManyInIdsWithDel(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdAdd)];
		R("SlctManyInIds_Insert_Multi", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var ents = new List<TestKv>();
				for (var i = 0; i < 3; i++) {
					ents.Add(new TestKv{
						Id = new IdTestKv(),
						Owner = IdTestUser.Zero,
						KStr = "slct_many_k_" + System.Guid.NewGuid().ToString("N"),
						VStr = "slct_many_v_" + System.Guid.NewGuid().ToString("N"),
					});
				}

				var resp = await Repo.OrdAdd(Ctx, AsyE(ents.ToArray()), CT.None);
				T(resp is not null, "BatAdd returned null response");

				_slctManyIds.Clear();
				_slctManyIds.AddRange(ents.Select(x=>x.Id));
				_slctManySoftDeletedId = _slctManyIds[0];
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.SoftDelInId)];
		R("SlctManyInIds_SoftDelete_One", async(o)=>{
			if (_slctManySoftDeletedId is null) {
				T(false, "SlctManyInIds_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.SoftDelInId(Ctx, AsyE(_slctManySoftDeletedId!.Value), CT.None);
				T(resp is not null, "SoftDelInId returned null response");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.GetInId)];
		R("SlctManyInIds_NonWithDel_Exclude_SoftDeleted", async(o)=>{
			if (_slctManyIds.Count == 0 || _slctManySoftDeletedId is null) {
				T(false, "SlctManyInIds_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var result = Repo.GetInId(Ctx, AsyE(_slctManyIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in result) {
					list.Add(item);
				}

				var gotIds = list.Where(x=>x is not null).Select(x=>x!.Id).ToHashSet();
				T(!gotIds.Contains(_slctManySoftDeletedId!.Value), "GetManyInId should not return soft-deleted rows");
				T(gotIds.Count == _slctManyIds.Count - 1, $"Expected {_slctManyIds.Count - 1} non-deleted rows, got {gotIds.Count}");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.GetInIdWithDel)];
		R("SlctManyInIds_WithDel_Include_SoftDeleted", async(o)=>{
			if (_slctManyIds.Count == 0 || _slctManySoftDeletedId is null) {
				T(false, "SlctManyInIds_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var result = Repo.GetInIdWithDel(Ctx, AsyE(_slctManyIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in result) {
					list.Add(item);
				}

				var gotIds = list.Where(x=>x is not null).Select(x=>x!.Id).ToHashSet();
				T(gotIds.Count == _slctManyIds.Count, $"Expected {_slctManyIds.Count} rows, got {gotIds.Count}");

				var del = list.FirstOrDefault(x=>x is not null && x.Id.Equals(_slctManySoftDeletedId!.Value));
				T(del is not null, "GetManyInIdWithDel should include soft-deleted row");
				T(del is not null && del.DelAt != 0, "Returned soft-deleted row should be marked deleted");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.GetInIdWithDel)];
		R("SlctManyInIdsWithDel_EmptyIds_ReturnsEmpty", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var Result = Repo.GetInIdWithDel(Ctx, AsyE<IdTestKv>(), CT.None);
				var List = new List<TestKv?>();
				await foreach (var Item in Result) List.Add(Item);
				T(List.Count == 0, $"Expected empty, got {List.Count}");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.GetInIdWithDel)];
		R("SlctManyInIdsWithDel_NonExistIds_ReturnsEmpty", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var Result = Repo.GetInIdWithDel(Ctx, AsyE(new IdTestKv(), new IdTestKv()), CT.None);
				var List = new List<TestKv?>();
				await foreach (var Item in Result) List.Add(Item);
				T(List.Count == 0, $"Expected empty for non-existent ids, got {List.Count}");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdHardDelById)];
		R("SlctManyInIds_Cleanup_HardDelete", async(o)=>{
			if (_slctManyIds.Count == 0) {
				return NIL;
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				await Repo.OrdHardDelById(Ctx, AsyE(_slctManyIds.ToArray()), CT.None);
				return NIL;
			});
		});
	}
}
