using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<TestKv> _batInsertEnts = new();
	readonly List<IdTestKv> _batInsertIds = new();

	void RegisterBatInsert(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdAdd)];
		R("BatInsert_Insert_Multi", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var ents = new List<TestKv>();
				for (var i = 0; i < 3; i++) {
					var e = new TestKv{
						Id = new IdTestKv(),
						Owner = IdTestUser.Zero,
						KStr = "bat_insert_k_" + System.Guid.NewGuid().ToString("N"),
						VStr = "bat_insert_v_" + System.Guid.NewGuid().ToString("N"),
					};
					ents.Add(e);
				}

				var resp = await Repo.OrdAdd(Ctx, AsyE(ents.ToArray()), CT.None);
				T(resp is not null, "BatInsert returned null response");

				_batInsertEnts.Clear();
				_batInsertIds.Clear();
				_batInsertEnts.AddRange(ents);
				_batInsertIds.AddRange(ents.Select(x=>x.Id));
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)];
		R("BatInsert_Verify_BatSlctById", async(o)=>{
			if (_batInsertIds.Count == 0) {
				T(false, "BatInsert_Insert_Multi not executed or no ids recorded");
			}

			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var result = Repo.OrdGetByIdWithDel(Ctx, AsyE(_batInsertIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in result) list.Add(item);

				T(list.Count == _batInsertIds.Count, $"Expected {_batInsertIds.Count} entries, got {list.Count}");

				for (var i = 0; i < list.Count; i++) {
					var got = list[i];
					T(got is not null, $"Expected non-null entity at index {i}");
					if (got is null) {
						continue;
					}
					var exp = _batInsertEnts[i];
					T(got.Id.Equals(exp.Id), $"Id mismatch at index {i}");
					T(got.KStr == exp.KStr && got.VStr == exp.VStr, $"Value mismatch at index {i}");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.GetInIdWithDel)];
		R("BatInsert_Verify_SlctManyInIdsWithDel", async(o)=>{
			if (_batInsertIds.Count == 0) {
				T(false, "BatInsert_Insert_Multi not executed or no ids recorded");
			}

			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var result = Repo.GetInIdWithDel(Ctx, AsyE(_batInsertIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in result) list.Add(item);

				T(list.Count > 0, "Expected non-empty result");

				var expected = new HashSet<IdTestKv>(_batInsertIds);
				foreach (var item in list) {
					T(item is not null, "Expected non-null entity");
					if (item is null) {
						continue;
					}
					expected.Remove(item.Id);
				}
				T(expected.Count == 0, $"Missing {expected.Count} inserted ids in SlctManyInIdsWithDel");
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.OrdHardDelById)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("BatInsert_Cleanup_HardDelete", async(o)=>{
			if (_batInsertIds.Count == 0) {
				return NIL;
			}

			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.OrdHardDelById(Ctx, AsyE(_batInsertIds.ToArray()), CT.None);
				T(resp is not null, "BatHardDelById returned null response");

				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_batInsertIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				T(!list.Any(x=>x != null), "Expected all nulls after hard delete");
				return NIL;
			});
		});
	}
}
