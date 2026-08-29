using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<IdTestKv> _batSlctIds = new();
	IdTestKv? _batSlctSoftDeletedId = null;

	void RegisterBatSlctById(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdAdd)];
		R("BatSlctById_Insert_Multi", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var ents = new List<TestKv>();
				for (var i = 0; i < 3; i++) {
					ents.Add(new TestKv{
						Id = new IdTestKv(),
						Owner = IdTestUser.Zero,
						KStr = "bat_slct_k_" + System.Guid.NewGuid().ToString("N"),
						VStr = "bat_slct_v_" + System.Guid.NewGuid().ToString("N"),
					});
				}

				var resp = await Repo.OrdAdd(Ctx, AsyE(ents.ToArray()), CT.None);
				T(resp is not null, "BatAdd returned null response");

				_batSlctIds.Clear();
				_batSlctIds.AddRange(ents.Select(x=>x.Id));
				_batSlctSoftDeletedId = _batSlctIds[0];
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.SoftDelInId)];
		R("BatSlctById_SoftDelete_One", async(o)=>{
			if (_batSlctSoftDeletedId is null) {
				T(false, "BatSlctById_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.SoftDelInId(Ctx, AsyE(_batSlctSoftDeletedId!.Value), CT.None);
				T(resp is not null, "SoftDelInId returned null response");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdGetById)];
		R("BatSlctById_NonWithDel_Should_Return_Null_For_SoftDeleted", async(o)=>{
			if (_batSlctIds.Count == 0 || _batSlctSoftDeletedId is null) {
				T(false, "BatSlctById_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var result = Repo.OrdGetById(Ctx, AsyE(_batSlctIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in result) {
					list.Add(item);
				}
				T(list.Count == _batSlctIds.Count, $"Expected {_batSlctIds.Count} entries, got {list.Count}");

				var softIdx = _batSlctIds.FindIndex(x=>x.Equals(_batSlctSoftDeletedId!.Value));
				T(softIdx >= 0, "soft-deleted id not found in test ids");
				T(list[softIdx] is null, "BatGetById should return null for soft-deleted row");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)];
		R("BatSlctById_EmptyIds_ReturnsEmpty", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var Result = Repo.OrdGetByIdWithDel(Ctx, AsyE<IdTestKv>(), CT.None);
				var List = new List<TestKv?>();
				await foreach (var Item in Result) List.Add(Item);
				T(List.Count == 0, $"Expected empty, got {List.Count}");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)];
		R("BatSlctById_WithDel_Should_Return_SoftDeleted", async(o)=>{
			if (_batSlctIds.Count == 0 || _batSlctSoftDeletedId is null) {
				T(false, "BatSlctById_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var result = Repo.OrdGetByIdWithDel(Ctx, AsyE(_batSlctIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in result) {
					list.Add(item);
				}
				T(list.Count == _batSlctIds.Count, $"Expected {_batSlctIds.Count} entries, got {list.Count}");

				var softIdx = _batSlctIds.FindIndex(x=>x.Equals(_batSlctSoftDeletedId!.Value));
				T(softIdx >= 0, "soft-deleted id not found in test ids");
				var softOne = list[softIdx];
				T(softOne is not null, "BatGetByIdWithDel should return soft-deleted row");
				T(softOne is not null && softOne.DelAt != 0, "Returned soft-deleted row should be marked deleted");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)];
		R("BatSlctById_NonExistIds_ReturnsNulls", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var Id1 = new IdTestKv();
				var Id2 = new IdTestKv();
				var Result = Repo.OrdGetByIdWithDel(Ctx, AsyE(Id1, Id2), CT.None);
				var List = new List<TestKv?>();
				await foreach (var Item in Result) List.Add(Item);
				T(List.Count == 2, $"Expected 2 entries (one per id), got {List.Count}");
				T(!List.Any(x => x != null), "Expected all nulls for non-existent IDs");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdHardDelById)];
		R("BatSlctById_Cleanup_HardDelete", async(o)=>{
			if (_batSlctIds.Count == 0) {
				return NIL;
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				await Repo.OrdHardDelById(Ctx, AsyE(_batSlctIds.ToArray()), CT.None);
				return NIL;
			});
		});
	}
}
