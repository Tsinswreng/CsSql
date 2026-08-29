using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<TestKv> _delInIdEnts = new();
	readonly List<IdTestKv> _delInIdIds = new();

	void RegisterDelInId(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdAdd)];
		R("DelInId_Insert_Multi", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var ents = new List<TestKv>();
				for (var i = 0; i < 3; i++) {
					var e = new TestKv{
						Id = new IdTestKv(),
						Owner = IdTestUser.Zero,
						KStr = "del_in_id_k_" + System.Guid.NewGuid().ToString("N"),
						VStr = "del_in_id_v_" + System.Guid.NewGuid().ToString("N"),
					};
					ents.Add(e);
				}

				var resp = await Repo.OrdAdd(Ctx, AsyE(ents.ToArray()), CT.None);
				T(resp is not null, "BatInsert returned null response");

				_delInIdEnts.Clear();
				_delInIdIds.Clear();
				_delInIdEnts.AddRange(ents);
				_delInIdIds.AddRange(ents.Select(x=>x.Id));
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.SoftDelInId)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("SoftDelInId", async(o)=>{
			if (_delInIdIds.Count == 0) {
				T(false, "DelInId_Insert_Multi not executed or no ids recorded");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.SoftDelInId(Ctx, AsyE(_delInIdIds.ToArray()), CT.None);
				T(resp is not null, "SoftDelInId returned null response");
				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_delInIdIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				T(list.Count == _delInIdIds.Count, $"Expected {_delInIdIds.Count} entries, got {list.Count}");
				for (var i = 0; i < list.Count; i++) {
					var got = list[i];
					T(got is not null, $"Expected non-null entity at index {i}");
					T(got is not null && got.DelAt != 0, $"Expected IsDeleted at index {i}");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.HardDelInId)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("HardDelInId", async(o)=>{
			if (_delInIdIds.Count == 0) {
				T(false, "DelInId_Insert_Multi not executed or no ids recorded");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.HardDelInId(Ctx, AsyE(_delInIdIds.ToArray()), CT.None);
				T(resp is not null, "HardDelInId returned null response");
				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_delInIdIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				T(!list.Any(x=>x != null), "Expected all nulls after hard delete");
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.OrdHardDelById)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("DelInId_Cleanup_HardDelete", async(o)=>{
			if (_delInIdIds.Count == 0) {
				return NIL;
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.OrdHardDelById(Ctx, AsyE(_delInIdIds.ToArray()), CT.None);
				T(resp is not null, "BatHardDelById returned null response");
				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_delInIdIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				T(!list.Any(x=>x != null), "Expected all nulls after hard delete");
				return NIL;
			});
		});
	}
}
